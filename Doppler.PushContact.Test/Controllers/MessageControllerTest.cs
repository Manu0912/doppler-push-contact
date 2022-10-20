using AutoFixture;
using Doppler.PushContact.Models;
using Doppler.PushContact.Services;
using Doppler.PushContact.Services.Messages;
using Doppler.PushContact.Test.Controllers.Utils;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Doppler.PushContact.Test.Controllers
{
    public class MessageControllerTest : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;
        private readonly ITestOutputHelper _output;

        public MessageControllerTest(WebApplicationFactory<Startup> factory, ITestOutputHelper output)
        {
            _factory = factory;
            _output = output;
        }

        [Theory]
        [InlineData(TestApiUsersData.TOKEN_EMPTY)]
        [InlineData(TestApiUsersData.TOKEN_BROKEN)]
        public async Task MessageByVisitorGuid_should_return_unauthorized_when_token_is_not_valid(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var visitorGuid = fixture.Create<string>();
            var messageId = fixture.Create<Guid>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"message/{messageId}")
            {
                Headers = { { "Authorization", $"Bearer {token}" } },
                Content = JsonContent.Create(visitorGuid)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData(TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20010908)]
        public async Task MessageByVisitorGuid_should_return_unauthorized_when_token_is_a_expired_superuser_token(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var visitorGuid = fixture.Create<string>();
            var messageId = fixture.Create<Guid>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"message/{messageId}")
            {
                Headers = { { "Authorization", $"Bearer {token}" } },
                Content = JsonContent.Create(visitorGuid)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData(TestApiUsersData.TOKEN_EXPIRE_20330518)]
        [InlineData(TestApiUsersData.TOKEN_SUPERUSER_FALSE_EXPIRE_20330518)]
        [InlineData(TestApiUsersData.TOKEN_ACCOUNT_123_TEST1_AT_TEST_DOT_COM_EXPIRE_20330518)]
        public async Task MessageByVisitorGuid_should_require_a_valid_token_with_isSU_flag(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var visitorGuid = fixture.Create<string>();
            var messageId = fixture.Create<Guid>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"message/{messageId}")
            {
                Headers = { { "Authorization", $"Bearer {token}" } },
                Content = JsonContent.Create(visitorGuid)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task MessageByVisitorGuid_should_return_unauthorized_when_authorization_header_is_empty()
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var visitorGuid = fixture.Create<string>();
            var messageId = fixture.Create<Guid>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"message/{messageId}")
            {
                Content = JsonContent.Create(visitorGuid)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task MessageByVisitorGuid_should_return_internal_server_error_when_service_throw_an_exception()
        {
            // Arrange
            var fixture = new Fixture();

            var visitorGuid = fixture.Create<string>();
            var domain = fixture.Create<string>();
            var messageId = fixture.Create<Guid>();

            var messageRepositoryMock = new Mock<IMessageRepository>();

            messageRepositoryMock
                .Setup(x => x.GetMessageDetailsAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .ThrowsAsync(new Exception());

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Post, $"message/{messageId}")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(visitorGuid)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Theory]
        [InlineData(" ", "ccf7ad9b-bd9a-465a-b240-602c93140bf3")]
        public async Task MessageByVisitorGuid_should_return_bad_request_when_messageId_is_whitespace(string visitorGuid, string messageId)
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();
            var messageSenderMock = new Mock<IMessageSender>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Post, $"message/{messageId}")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(visitorGuid)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData("", "exampleMessageId")]
        [InlineData(" ", "exampleMessageId")]
        [InlineData(null, "exampleMessageId")]
        public async Task MessageByVisitorGuid_should_return_bad_request_when_visitor_guid_is_null_empty_or_whitespace(string visitorGuid, string messageId)
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();
            var messageSenderMock = new Mock<IMessageSender>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Post, $"message/{messageId}")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(visitorGuid)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData(TestApiUsersData.TOKEN_EMPTY)]
        [InlineData(TestApiUsersData.TOKEN_BROKEN)]
        public async Task CreateMessage_should_return_unauthorized_when_token_is_not_valid(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var name = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"message")
            {
                Headers = { { "Authorization", $"Bearer {token}" } }
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData(TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20010908)]
        public async Task CreateMessage_should_return_unauthorized_when_token_is_a_expired_superuser_token(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Post, $"message")
            {
                Headers = { { "Authorization", $"Bearer {token}" } }
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData(TestApiUsersData.TOKEN_EXPIRE_20330518)]
        [InlineData(TestApiUsersData.TOKEN_SUPERUSER_FALSE_EXPIRE_20330518)]
        [InlineData(TestApiUsersData.TOKEN_ACCOUNT_123_TEST1_AT_TEST_DOT_COM_EXPIRE_20330518)]
        public async Task CreateMessage_should_require_a_valid_token_with_isSU_flag(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Post, $"message")
            {
                Headers = { { "Authorization", $"Bearer {token}" } }
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task CreateMessage_should_return_unauthorized_when_authorization_header_is_empty()
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Post, $"message");

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CreateMessage_should_create_a_message_with_summarization_fields_equal_cero_and_return_proper_messageId()
        {
            // Arrange
            var fixture = new Fixture();

            var qSent = 0;
            var qDelivery = 0;
            var qNotDelivery = 0;

            var message = new MessageBody
            {
                Message = new Message()
                {
                    Title = fixture.Create<string>(),
                    Body = fixture.Create<string>(),
                    OnClickLink = fixture.Create<string>(),
                    ImageUrl = fixture.Create<string>()
                },
                Domain = fixture.Create<string>()
            };

            var pushContactService = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();
            var messageSenderMock = new Mock<IMessageSender>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactService.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Post, $"message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            messageRepositoryMock.Verify(mock => mock.AddAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                qSent,
                qDelivery,
                qNotDelivery,
                It.IsAny<string>()
            ), Times.Once());
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var messageResult = await response.Content.ReadFromJsonAsync<MessageResult>();
            Assert.IsType<Guid>(messageResult.MessageId);
        }

        [Fact]
        public async Task CreateMessage_should_return_UnprocessableEntity_error_when_messageSender_throw_an_exception()
        {
            // Arrange
            var fixture = new Fixture();

            var message = new MessageBody
            {
                Message = new Message()
                {
                    Title = fixture.Create<string>(),
                    Body = fixture.Create<string>()
                },
                Domain = fixture.Create<string>()
            };

            var pushContactService = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();
            var messageSenderMock = new Mock<IMessageSender>();

            messageSenderMock
                .Setup(x => x.ValidateMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new ArgumentException());

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactService.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Post, $"message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }

        [Theory]
        [InlineData("", "aTitle", "aBody")]
        [InlineData(" ", "aTitle", "aBody")]
        [InlineData(null, "aTitle", "aBody")]
        [InlineData("domain.com", "", "aBody")]
        [InlineData("domain.com", " ", "aBody")]
        [InlineData("domain.com", null, "aBody")]
        [InlineData("domain.com", "aTitle", "")]
        [InlineData("domain.com", "aTitle", " ")]
        [InlineData("domain.com", "aTitle", null)]
        public async Task CreateMessage_should_return_BadRequest_error_when_domain_or_title_or_body_are_missing(string domain, string title, string body)
        {
            // Arrange
            var message = new MessageBody
            {
                Message = new Message()
                {
                    Title = title,
                    Body = body
                },
                Domain = domain
            };

            var pushContactService = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();
            var messageSenderMock = new Mock<IMessageSender>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactService.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Post, $"message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateMessage_should_return_BadRequest_error_when_message_field_is_missing()
        {
            var fixture = new Fixture();

            // Arrange
            var message = new MessageBody
            {
                Domain = fixture.Create<string>()
            };

            var pushContactService = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();
            var messageSenderMock = new Mock<IMessageSender>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactService.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Post, $"message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}

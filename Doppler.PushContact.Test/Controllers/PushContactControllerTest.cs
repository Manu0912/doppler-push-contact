using AutoFixture;
using Doppler.PushContact.ApiModels;
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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Doppler.PushContact.Test.Controllers
{
    public class PushContactControllerTest : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;
        private readonly ITestOutputHelper _output;

        public PushContactControllerTest(WebApplicationFactory<Startup> factory, ITestOutputHelper output)
        {
            _factory = factory;
            _output = output;
        }

        [Fact]
        public async Task Add_should_not_require_token()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Post, "push-contacts")
            {
                Content = JsonContent.Create(fixture.Create<PushContactModel>())
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData(TestApiUsersData.TOKEN_EMPTY)]
        [InlineData(TestApiUsersData.TOKEN_BROKEN)]
        [InlineData(TestApiUsersData.TOKEN_EXPIRE_20330518)]
        [InlineData(TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518)]
        [InlineData(TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20010908)]
        [InlineData(TestApiUsersData.TOKEN_SUPERUSER_FALSE_EXPIRE_20330518)]
        [InlineData(TestApiUsersData.TOKEN_ACCOUNT_123_TEST1_AT_TEST_DOT_COM_EXPIRE_20330518)]
        public async Task Add_should_accept_any_token(string token)
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Post, "push-contacts")
            {
                Headers = { { "Authorization", $"Bearer {token}" } },
                Content = JsonContent.Create(fixture.Create<PushContactModel>())
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory(Skip = "Now allows anonymous")]
        [InlineData(TestApiUsersData.TOKEN_EMPTY)]
        [InlineData(TestApiUsersData.TOKEN_BROKEN)]
        public async Task Add_should_return_unauthorized_when_token_is_not_valid(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Post, "push-contacts")
            {
                Headers = { { "Authorization", $"Bearer {token}" } }
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory(Skip = "Now allows anonymous")]
        [InlineData(TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20010908)]
        public async Task Add_should_return_unauthorized_when_token_is_a_expired_superuser_token(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Post, "push-contacts")
            {
                Headers = { { "Authorization", $"Bearer {token}" } }
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory(Skip = "Now allows anonymous")]
        [InlineData(TestApiUsersData.TOKEN_EXPIRE_20330518)]
        [InlineData(TestApiUsersData.TOKEN_SUPERUSER_FALSE_EXPIRE_20330518)]
        [InlineData(TestApiUsersData.TOKEN_ACCOUNT_123_TEST1_AT_TEST_DOT_COM_EXPIRE_20330518)]
        public async Task Add_should_require_a_valid_token_with_isSU_flag(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Post, "push-contacts")
            {
                Headers = { { "Authorization", $"Bearer {token}" } }
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact(Skip = "Now allows anonymous")]
        public async Task Add_should_return_unauthorized_when_authorization_header_is_empty()
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Post, "push-contacts");

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData(false, HttpStatusCode.OK)]
        [InlineData(true, HttpStatusCode.InternalServerError)]
        public async Task Add_should_return_expected_status_code_depending_on_service_add_result
            (bool addResultWithException, HttpStatusCode expectedHttpStatusCode)
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            pushContactServiceMock
                .Setup(x => x.AddAsync(It.IsAny<PushContactModel>()))
                .Returns(addResultWithException ? Task.FromException(new Exception()) : Task.CompletedTask);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Post, "push-contacts")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(fixture.Create<PushContactModel>())
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(expectedHttpStatusCode, response.StatusCode);
        }

        [Fact]
        public async Task UpdateEmail_should_not_require_token()
        {
            // Arrange
            var fixture = new Fixture();

            var deviceToken = fixture.Create<string>();
            var email = fixture.Create<string>();

            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Put, $"push-contacts/{deviceToken}/email")
            {
                Content = JsonContent.Create(email)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData(TestApiUsersData.TOKEN_EMPTY)]
        [InlineData(TestApiUsersData.TOKEN_BROKEN)]
        [InlineData(TestApiUsersData.TOKEN_EXPIRE_20330518)]
        [InlineData(TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518)]
        [InlineData(TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20010908)]
        [InlineData(TestApiUsersData.TOKEN_SUPERUSER_FALSE_EXPIRE_20330518)]
        [InlineData(TestApiUsersData.TOKEN_ACCOUNT_123_TEST1_AT_TEST_DOT_COM_EXPIRE_20330518)]
        public async Task UpdateEmail_should_accept_any_token(string token)
        {
            // Arrange
            var fixture = new Fixture();

            var deviceToken = fixture.Create<string>();
            var email = fixture.Create<string>();

            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Put, $"push-contacts/{deviceToken}/email")
            {
                Headers = { { "Authorization", $"Bearer {token}" } },
                Content = JsonContent.Create(email)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory(Skip = "Now allows anonymous")]
        [InlineData(TestApiUsersData.TOKEN_EMPTY)]
        [InlineData(TestApiUsersData.TOKEN_BROKEN)]
        public async Task UpdateEmail_should_return_unauthorized_when_token_is_not_valid(string token)
        {
            // Arrange
            var fixture = new Fixture();

            var deviceToken = fixture.Create<string>();
            var email = fixture.Create<string>();

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Put, $"push-contacts/{deviceToken}/email")
            {
                Headers = { { "Authorization", $"Bearer {token}" } },
                Content = JsonContent.Create(email)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory(Skip = "Now allows anonymous")]
        [InlineData(TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20010908)]
        public async Task UpdateEmail_should_return_unauthorized_when_token_is_a_expired_superuser_token(string token)
        {
            // Arrange
            var fixture = new Fixture();

            var deviceToken = fixture.Create<string>();
            var email = fixture.Create<string>();

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Put, $"push-contacts/{deviceToken}/email")
            {
                Headers = { { "Authorization", $"Bearer {token}" } },
                Content = JsonContent.Create(email)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory(Skip = "Now allows anonymous")]
        [InlineData(TestApiUsersData.TOKEN_EXPIRE_20330518)]
        [InlineData(TestApiUsersData.TOKEN_SUPERUSER_FALSE_EXPIRE_20330518)]
        [InlineData(TestApiUsersData.TOKEN_ACCOUNT_123_TEST1_AT_TEST_DOT_COM_EXPIRE_20330518)]
        public async Task UpdateEmail_should_require_a_valid_token_with_isSU_flag(string token)
        {
            // Arrange
            var fixture = new Fixture();

            var deviceToken = fixture.Create<string>();
            var email = fixture.Create<string>();

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Put, $"push-contacts/{deviceToken}/email")
            {
                Headers = { { "Authorization", $"Bearer {token}" } },
                Content = JsonContent.Create(email)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact(Skip = "Now allows anonymous")]
        public async Task UpdateEmail_should_return_unauthorized_when_authorization_header_is_empty()
        {
            // Arrange
            var fixture = new Fixture();

            var deviceToken = fixture.Create<string>();
            var email = fixture.Create<string>();

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Put, $"push-contacts/{deviceToken}/email")
            {
                Content = JsonContent.Create(email)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task UpdateEmail_should_return_ok_when_service_does_not_throw_an_exception()
        {
            // Arrange
            var fixture = new Fixture();

            var deviceToken = fixture.Create<string>();
            var email = fixture.Create<string>();

            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            pushContactServiceMock
                .Setup(x => x.UpdateEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Put, $"push-contacts/{deviceToken}/email")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(email)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task UpdateEmail_should_return_internal_server_error_when_service_throw_an_exception()
        {
            // Arrange
            var fixture = new Fixture();

            var deviceToken = fixture.Create<string>();
            var email = fixture.Create<string>();

            var pushContactServiceMock = new Mock<IPushContactService>();

            pushContactServiceMock
                .Setup(x => x.UpdateEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception());

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Put, $"push-contacts/{deviceToken}/email")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(email)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Theory]
        [InlineData(TestApiUsersData.TOKEN_EMPTY)]
        [InlineData(TestApiUsersData.TOKEN_BROKEN)]
        public async Task UpdatePushContactVisitorGuid_should_return_unauthorized_when_token_is_not_valid(string token)
        {
            // Arrange
            var fixture = new Fixture();

            var deviceToken = fixture.Create<string>();
            var visitorGuid = fixture.Create<string>();

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Put, $"push-contacts/{deviceToken}/visitor-guid")
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
        public async Task UpdatePushContactVisitorGuid_should_return_unauthorized_when_token_is_a_expired_superuser_token(string token)
        {
            // Arrange
            var fixture = new Fixture();

            var deviceToken = fixture.Create<string>();
            var visitorGuid = fixture.Create<string>();

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Put, $"push-contacts/{deviceToken}/visitor-guid")
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
        public async Task UpdatePushContactVisitorGuid_should_require_a_valid_token_with_isSU_flag(string token)
        {
            // Arrange
            var fixture = new Fixture();

            var deviceToken = fixture.Create<string>();
            var visitorGuid = fixture.Create<string>();

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Put, $"push-contacts/{deviceToken}/visitor-guid")
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
        public async Task UpdatePushContactVisitorGuid_should_return_unauthorized_when_authorization_header_is_empty()
        {
            // Arrange
            var fixture = new Fixture();

            var deviceToken = fixture.Create<string>();
            var visitorGuid = fixture.Create<string>();

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Put, $"push-contacts/{deviceToken}/visitor-guid")
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
        public async Task UpdatePushContactVisitorGuid_should_return_ok_when_service_does_not_throw_an_exception()
        {
            // Arrange
            var fixture = new Fixture();

            var deviceToken = fixture.Create<string>();
            var visitorGuid = fixture.Create<string>();

            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            pushContactServiceMock
                .Setup(x => x.UpdateEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Put, $"push-contacts/{deviceToken}/visitor-guid")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(visitorGuid)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task UpdatePushContactVisitorGuid_should_return_internal_server_error_when_service_throw_an_exception()
        {
            // Arrange
            var fixture = new Fixture();

            var deviceToken = fixture.Create<string>();
            var visitorGuid = fixture.Create<string>();

            var pushContactServiceMock = new Mock<IPushContactService>();

            pushContactServiceMock
                .Setup(x => x.UpdateEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception());

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Put, $"push-contacts/{deviceToken}/visitor-guid")
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
        [InlineData(" ", "")]
        [InlineData("example_device_token", "")]
        [InlineData("example_device_token", " ")]
        [InlineData("example_device_token", null)]
        [InlineData(" ", "example_visitor_guid")]
        public async Task UpdatePushContactVisitorGuid_should_return_bad_request_when_device_token_or_visitor_guid_are_empty_or_hite_space(string deviceToken, string visitorGuid)
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            pushContactServiceMock
                .Setup(x => x.UpdateEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Put, $"push-contacts/{deviceToken}/visitor-guid")
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
        public async Task GetBy_should_return_unauthorized_when_token_is_not_valid(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var domain = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts?domain={domain}")
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
        public async Task GetBy_should_return_unauthorized_when_token_is_a_expired_superuser_token(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var domain = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts?domain={domain}")
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
        public async Task GetBy_should_require_a_valid_token_with_isSU_flag(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var domain = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts?domain={domain}")
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
        public async Task GetBy_should_return_unauthorized_when_authorization_header_is_empty()
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var domain = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts?domain={domain}");

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetBy_should_return_push_contacts_that_service_get_method_return()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContacts = fixture.CreateMany<PushContactModel>(10);

            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            pushContactServiceMock
                .Setup(x => x.GetAsync(It.IsAny<PushContactFilter>()))
                .ReturnsAsync(pushContacts);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var email = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts?domain={domain}&email={email}")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } }
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var responseAsString = await response.Content.ReadAsStringAsync();
            var pushContactsResponse = JsonSerializer.Deserialize<IEnumerable<PushContactModel>>
                (responseAsString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.Equal(pushContacts.Count(), pushContactsResponse.Count());

            var pushContactsEnumerator = pushContacts.GetEnumerator();
            var pushContactsResponseEnumerator = pushContactsResponse.GetEnumerator();

            while (pushContactsEnumerator.MoveNext() && pushContactsResponseEnumerator.MoveNext())
            {
                Assert.True(pushContactsEnumerator.Current.Domain == pushContactsResponseEnumerator.Current.Domain);
                Assert.True(pushContactsEnumerator.Current.DeviceToken == pushContactsResponseEnumerator.Current.DeviceToken);
                Assert.True(pushContactsEnumerator.Current.Email == pushContactsResponseEnumerator.Current.Email);
            }
        }

        [Fact]
        public async Task GetBy_should_return_not_found_when_service_get_method_return_a_empty_push_contacts_collection()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContacts = Enumerable.Empty<PushContactModel>();

            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            pushContactServiceMock
                .Setup(x => x.GetAsync(It.IsAny<PushContactFilter>()))
                .ReturnsAsync(pushContacts);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var email = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts?domain={domain}&email={email}")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } }
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetBy_should_return_not_found_when_service_get_method_return_null()
        {
            // Arrange
            var fixture = new Fixture();

            IEnumerable<PushContactModel> pushContacts = null;

            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            pushContactServiceMock
                .Setup(x => x.GetAsync(It.IsAny<PushContactFilter>()))
                .ReturnsAsync(pushContacts);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var email = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts?domain={domain}&email={email}")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } }
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetBy_should_return_bad_request_when_domain_param_is_not_in_query_string()
        {
            // Arrange
            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } }
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory(Skip = "Endpoint removed")]
        [InlineData(TestApiUsersData.TOKEN_EMPTY)]
        [InlineData(TestApiUsersData.TOKEN_BROKEN)]
        public async Task BulkDelete_should_return_unauthorized_when_token_is_not_valid(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Delete, "push-contacts/_bulk")
            {
                Headers = { { "Authorization", $"Bearer {token}" } }
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory(Skip = "Endpoint removed")]
        [InlineData(TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20010908)]
        public async Task BulkDelete_should_return_unauthorized_when_token_is_a_expired_superuser_token(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Delete, "push-contacts/_bulk")
            {
                Headers = { { "Authorization", $"Bearer {token}" } }
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory(Skip = "Endpoint removed")]
        [InlineData(TestApiUsersData.TOKEN_EXPIRE_20330518)]
        [InlineData(TestApiUsersData.TOKEN_SUPERUSER_FALSE_EXPIRE_20330518)]
        [InlineData(TestApiUsersData.TOKEN_ACCOUNT_123_TEST1_AT_TEST_DOT_COM_EXPIRE_20330518)]
        public async Task BulkDelete_should_require_a_valid_token_with_isSU_flag(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Delete, "push-contacts/_bulk")
            {
                Headers = { { "Authorization", $"Bearer {token}" } }
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact(Skip = "Endpoint removed")]
        public async Task BulkDelete_should_return_unauthorized_when_authorization_header_is_empty()
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Delete, "push-contacts/_bulk");

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact(Skip = "Endpoint removed")]
        public async Task BulkDelete_should_return_ok_and_deleted_count_when_service_does_not_throw_an_exception()
        {
            // Arrange
            var fixture = new Fixture();

            var deletedCount = fixture.Create<int>();

            var pushContactServiceMock = new Mock<IPushContactService>();

            pushContactServiceMock
                .Setup(x => x.DeleteByDeviceTokenAsync(It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(deletedCount);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Delete, "push-contacts/_bulk")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(fixture.Create<IEnumerable<string>>())
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var responseAsString = await response.Content.ReadAsStringAsync();
            Assert.Equal(deletedCount.ToString(), responseAsString);
        }

        [Fact(Skip = "Endpoint removed")]
        public async Task BulkDelete_should_return_internal_server_error_when_service_throw_an_exception()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();

            pushContactServiceMock
                .Setup(x => x.DeleteByDeviceTokenAsync(It.IsAny<IEnumerable<string>>()))
                .ThrowsAsync(new Exception());

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Delete, "push-contacts/_bulk")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(fixture.Create<IEnumerable<string>>())
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Theory]
        [InlineData(TestApiUsersData.TOKEN_EMPTY)]
        [InlineData(TestApiUsersData.TOKEN_BROKEN)]
        public async Task Message_should_return_unauthorized_when_token_is_not_valid(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var domain = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/message")
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
        public async Task Message_should_return_unauthorized_when_token_is_a_expired_superuser_token(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var domain = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/message")
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
        public async Task Message_should_require_a_valid_token_with_isSU_flag(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var domain = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/message")
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
        public async Task Message_should_return_unauthorized_when_authorization_header_is_empty()
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var domain = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/message");

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData(null, "some body")]
        [InlineData("some title", null)]
        [InlineData(null, null)]
        [InlineData("", "some body")]
        [InlineData("some title", "")]
        [InlineData("", "")]
        public async Task Message_should_return_bad_request_when_title_or_body_are_missing(string title, string body)
        {
            // Arrange
            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = title,
                Body = body,
                OnClickLink = fixture.Create<string>()
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/message")
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
        public async Task Message_should_does_not_call_DeleteByDeviceTokenAsync_when_all_device_tokens_returned_by_message_sender_are_valid()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();

            var sendMessageResult = new SendMessageResult
            {
                SendMessageTargetResult = fixture.CreateMany<SendMessageTargetResult>(10)
            };
            sendMessageResult.SendMessageTargetResult.ToList().ForEach(x => x.IsValidTargetDeviceToken = true);

            var messageSenderMock = new Mock<IMessageSender>();
            messageSenderMock
                .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(sendMessageResult);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = fixture.Create<string>(),
                Body = fixture.Create<string>(),
                OnClickLink = fixture.Create<string>()
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            pushContactServiceMock
                .Verify(x => x.DeleteByDeviceTokenAsync(It.IsAny<IEnumerable<string>>()), Times.Never());
        }

        [Fact]
        public async Task Message_should_does_not_call_DeleteByDeviceTokenAsync_when_message_sender_returned_an_empty_target_result_collection()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();

            var sendMessageResult = new SendMessageResult
            {
                SendMessageTargetResult = new List<SendMessageTargetResult>()
            };

            var messageSenderMock = new Mock<IMessageSender>();
            messageSenderMock
                .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(sendMessageResult);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = fixture.Create<string>(),
                Body = fixture.Create<string>(),
                OnClickLink = fixture.Create<string>()
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            pushContactServiceMock
                .Verify(x => x.DeleteByDeviceTokenAsync(It.IsAny<IEnumerable<string>>()), Times.Never());
        }

        [Fact]
        public async Task Message_should_does_not_call_DeleteByDeviceTokenAsync_when_message_sender_returned_null_as_target_result_collection()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();

            var sendMessageResult = new SendMessageResult
            {
                SendMessageTargetResult = null
            };

            var messageSenderMock = new Mock<IMessageSender>();
            messageSenderMock
                .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(sendMessageResult);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = fixture.Create<string>(),
                Body = fixture.Create<string>(),
                OnClickLink = fixture.Create<string>()
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            pushContactServiceMock
                .Verify(x => x.DeleteByDeviceTokenAsync(It.IsAny<IEnumerable<string>>()), Times.Never());
        }

        [Fact]
        public async Task
            Message_should_does_not_call_AddHistoryEventsAsync_when_message_sender_returned_a_empty_target_result_collection()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();

            var sendMessageResult = new SendMessageResult
            {
                SendMessageTargetResult = Enumerable.Empty<SendMessageTargetResult>()
            };

            var messageSenderMock = new Mock<IMessageSender>();
            messageSenderMock
                .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(sendMessageResult);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = fixture.Create<string>(),
                Body = fixture.Create<string>(),
                OnClickLink = fixture.Create<string>()
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            pushContactServiceMock
                .Verify(x => x.AddHistoryEventsAsync(It.IsAny<IEnumerable<PushContactHistoryEvent>>()), Times.Never());
        }

        [Fact]
        public async Task
            Message_should_does_not_call_AddHistoryEventsAsync_when_message_sender_returned_a_null_target_result_collection()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();

            var sendMessageResult = new SendMessageResult
            {
                SendMessageTargetResult = null
            };

            var messageSenderMock = new Mock<IMessageSender>();
            messageSenderMock
                .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(sendMessageResult);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = fixture.Create<string>(),
                Body = fixture.Create<string>(),
                OnClickLink = fixture.Create<string>()
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            pushContactServiceMock
                .Verify(x => x.AddHistoryEventsAsync(It.IsAny<IEnumerable<PushContactHistoryEvent>>()), Times.Never());
        }

        [Fact]
        public async Task Message_should_return_ok_and_a_message_result_when_send_message_steps_do_not_throw_a_exception()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();
            pushContactServiceMock
            .Setup(x => x.GetAllDeviceTokensByDomainAsync(It.IsAny<string>()))
            .ReturnsAsync(fixture.Create<IEnumerable<string>>());

            pushContactServiceMock
            .Setup(x => x.DeleteByDeviceTokenAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(fixture.Create<int>());

            pushContactServiceMock
            .Setup(x => x.AddHistoryEventsAsync(It.IsAny<IEnumerable<PushContactHistoryEvent>>()))
            .Returns(Task.CompletedTask);

            var messageSenderMock = new Mock<IMessageSender>();
            messageSenderMock
                .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(fixture.Create<SendMessageResult>());

            var messageRepositoryMock = new Mock<IMessageRepository>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = fixture.Create<string>(),
                Body = fixture.Create<string>(),
                OnClickLink = fixture.Create<string>()
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            var messageResult = await response.Content.ReadFromJsonAsync<MessageResult>();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Message_should_return_internal_server_error_when_GetAllDeviceTokensByDomainAsync_throw_a_exception()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();
            pushContactServiceMock
                .Setup(x => x.GetAllDeviceTokensByDomainAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception());

            var messageSenderMock = new Mock<IMessageSender>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = fixture.Create<string>(),
                Body = fixture.Create<string>(),
                OnClickLink = fixture.Create<string>()
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task Message_should_return_internal_server_error_when_SendAsync_throw_a_exception()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();

            var messageSenderMock = new Mock<IMessageSender>();
            messageSenderMock
                .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception());

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = fixture.Create<string>(),
                Body = fixture.Create<string>(),
                OnClickLink = fixture.Create<string>()
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task Message_should_allow_missing_onClickLink_param(string onClickLink)
        {
            // Arrange
            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageSenderMock = new Mock<IMessageSender>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = fixture.Create<string>(),
                Body = fixture.Create<string>(),
                OnClickLink = onClickLink
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData(TestApiUsersData.TOKEN_EMPTY)]
        [InlineData(TestApiUsersData.TOKEN_BROKEN)]
        public async Task Message_By_Visitor_Guid_should_return_unauthorized_when_token_is_not_valid(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var domain = fixture.Create<string>();
            var visitorGuid = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/{visitorGuid}/message")
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
        public async Task Message_By_Visitor_Guid_should_return_unauthorized_when_token_is_a_expired_superuser_token(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var domain = fixture.Create<string>();
            var visitorGuid = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/{visitorGuid}/message")
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
        public async Task Message_By_Visitor_Guid_should_require_a_valid_token_with_isSU_flag(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var domain = fixture.Create<string>();
            var visitorGuid = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/{visitorGuid}/message")
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
        public async Task Message_By_Visitor_Guid_should_return_unauthorized_when_authorization_header_is_empty()
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var domain = fixture.Create<string>();
            var visitorGuid = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/{visitorGuid}/message");

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData(null, "some body")]
        [InlineData("some title", null)]
        [InlineData(null, null)]
        [InlineData("", "some body")]
        [InlineData("some title", "")]
        [InlineData("", "")]
        public async Task Message_By_Visitor_Guid_should_return_bad_request_when_title_or_body_are_missing(string title, string body)
        {
            // Arrange
            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = title,
                Body = body,
                OnClickLink = fixture.Create<string>()
            };
            var visitorGuid = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/{visitorGuid}/message")
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
        public async Task Message_By_Visitor_Guid_should_does_not_call_DeleteByDeviceTokenAsync_when_all_device_tokens_returned_by_message_sender_are_valid()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();

            var sendMessageResult = new SendMessageResult
            {
                SendMessageTargetResult = fixture.CreateMany<SendMessageTargetResult>(10)
            };
            sendMessageResult.SendMessageTargetResult.ToList().ForEach(x => x.IsValidTargetDeviceToken = true);

            var messageSenderMock = new Mock<IMessageSender>();
            messageSenderMock
                .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(sendMessageResult);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = fixture.Create<string>(),
                Body = fixture.Create<string>(),
                OnClickLink = fixture.Create<string>()
            };
            var visitorGuid = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/{visitorGuid}/message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            pushContactServiceMock
                .Verify(x => x.DeleteByDeviceTokenAsync(It.IsAny<IEnumerable<string>>()), Times.Never());
        }

        [Fact]
        public async Task Message_By_Visitor_Guid_should_does_not_call_DeleteByDeviceTokenAsync_when_message_sender_returned_an_empty_target_result_collection()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();

            var sendMessageResult = new SendMessageResult
            {
                SendMessageTargetResult = new List<SendMessageTargetResult>()
            };

            var messageSenderMock = new Mock<IMessageSender>();
            messageSenderMock
                .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(sendMessageResult);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = fixture.Create<string>(),
                Body = fixture.Create<string>(),
                OnClickLink = fixture.Create<string>()
            };
            var visitorGuid = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/{visitorGuid}/message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            pushContactServiceMock
                .Verify(x => x.DeleteByDeviceTokenAsync(It.IsAny<IEnumerable<string>>()), Times.Never());
        }

        [Fact]
        public async Task Message_By_Visitor_Guid_should_does_not_call_DeleteByDeviceTokenAsync_when_message_sender_returned_null_as_target_result_collection()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();

            var sendMessageResult = new SendMessageResult
            {
                SendMessageTargetResult = null
            };

            var messageSenderMock = new Mock<IMessageSender>();
            messageSenderMock
                .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(sendMessageResult);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = fixture.Create<string>(),
                Body = fixture.Create<string>(),
                OnClickLink = fixture.Create<string>()
            };
            var visitorGuid = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/{visitorGuid}/message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            pushContactServiceMock
                .Verify(x => x.DeleteByDeviceTokenAsync(It.IsAny<IEnumerable<string>>()), Times.Never());
        }

        [Fact]
        public async Task Message_By_Visitor_Guid_should_call_DeleteByDeviceTokenAsync_with_not_valid_target_device_tokens_returned_by_message_sender()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            var sendMessageResult = new SendMessageResult
            {
                SendMessageTargetResult = fixture.CreateMany<SendMessageTargetResult>(10)
            };
            sendMessageResult.SendMessageTargetResult.First().IsValidTargetDeviceToken = false;

            var messageSenderMock = new Mock<IMessageSender>();
            messageSenderMock
                .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(sendMessageResult);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = fixture.Create<string>(),
                Body = fixture.Create<string>(),
                OnClickLink = fixture.Create<string>()
            };
            var visitorGuid = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/{visitorGuid}/message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            pushContactServiceMock
                .Verify(x => x.DeleteByDeviceTokenAsync(
                    It.Is<IEnumerable<string>>(y => y.All(z => sendMessageResult.SendMessageTargetResult.Any(w => w.TargetDeviceToken == z && !w.IsValidTargetDeviceToken)))), Times.Once());
        }

        [Fact]
        public async Task Message_By_Visitor_Guid_should_does_not_call_AddHistoryEventsAsync_when_message_sender_returned_a_empty_target_result_collection()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();

            var sendMessageResult = new SendMessageResult
            {
                SendMessageTargetResult = Enumerable.Empty<SendMessageTargetResult>()
            };

            var messageSenderMock = new Mock<IMessageSender>();
            messageSenderMock
                .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(sendMessageResult);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = fixture.Create<string>(),
                Body = fixture.Create<string>(),
                OnClickLink = fixture.Create<string>()
            };
            var visitorGuid = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/{visitorGuid}/message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            pushContactServiceMock
                .Verify(x => x.AddHistoryEventsAsync(It.IsAny<IEnumerable<PushContactHistoryEvent>>()), Times.Never());
        }

        [Fact]
        public async Task Message_By_Visitor_Guid_should_does_not_call_AddHistoryEventsAsync_when_message_sender_returned_a_null_target_result_collection()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();

            var sendMessageResult = new SendMessageResult
            {
                SendMessageTargetResult = null
            };

            var messageSenderMock = new Mock<IMessageSender>();
            messageSenderMock
                .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(sendMessageResult);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = fixture.Create<string>(),
                Body = fixture.Create<string>(),
                OnClickLink = fixture.Create<string>()
            };
            var visitorGuid = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/{visitorGuid}/message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            pushContactServiceMock
                .Verify(x => x.AddHistoryEventsAsync(It.IsAny<IEnumerable<PushContactHistoryEvent>>()), Times.Never());
        }

        [Fact]
        public async Task Message_By_Visitor_Guid_should_call_AddHistoryEventsAsync_with_all_target_device_tokens_returned_by_message_sender()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            var sendMessageResult = new SendMessageResult
            {
                SendMessageTargetResult = fixture.CreateMany<SendMessageTargetResult>(10)
            };

            var messageSenderMock = new Mock<IMessageSender>();
            messageSenderMock
                .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(sendMessageResult);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = fixture.Create<string>(),
                Body = fixture.Create<string>(),
                OnClickLink = fixture.Create<string>()
            };
            var visitorGuid = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/{visitorGuid}/message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            pushContactServiceMock
                .Verify(x => x.AddHistoryEventsAsync(
                It.Is<IEnumerable<PushContactHistoryEvent>>(x => sendMessageResult.SendMessageTargetResult.All(y => x.Any(z => z.DeviceToken == y.TargetDeviceToken)))), Times.Once());
        }

        [Fact]
        public async Task Message_By_Visitor_Guid_should_return_ok_and_a_message_result_when_send_message_steps_do_not_throw_a_exception()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();
            pushContactServiceMock
            .Setup(x => x.GetAllDeviceTokensByDomainAsync(It.IsAny<string>()))
            .ReturnsAsync(fixture.Create<IEnumerable<string>>());

            pushContactServiceMock
            .Setup(x => x.DeleteByDeviceTokenAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(fixture.Create<int>());

            pushContactServiceMock
            .Setup(x => x.AddHistoryEventsAsync(It.IsAny<IEnumerable<PushContactHistoryEvent>>()))
            .Returns(Task.CompletedTask);

            var messageSenderMock = new Mock<IMessageSender>();
            messageSenderMock
                .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(fixture.Create<SendMessageResult>());

            var messageRepositoryMock = new Mock<IMessageRepository>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = fixture.Create<string>(),
                Body = fixture.Create<string>(),
                OnClickLink = fixture.Create<string>()
            };
            var visitorGuid = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/{visitorGuid}/message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            var messageResult = await response.Content.ReadFromJsonAsync<MessageResult>();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Message_By_Visitor_Guid_should_return_internal_server_error_when_GetAllDeviceTokensByDomainAsync_throw_a_exception()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();
            pushContactServiceMock
                .Setup(x => x.GetAllDeviceTokensByDomainAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception());

            var messageSenderMock = new Mock<IMessageSender>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = fixture.Create<string>(),
                Body = fixture.Create<string>(),
                OnClickLink = fixture.Create<string>()
            };
            var visitorGuid = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/{visitorGuid}/message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task Message_By_Visitor_Guid_should_return_internal_server_error_when_SendAsync_throw_a_exception()
        {
            // Arrange
            var fixture = new Fixture();

            var pushContactServiceMock = new Mock<IPushContactService>();

            var messageSenderMock = new Mock<IMessageSender>();
            messageSenderMock
                .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception());

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions());

            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = fixture.Create<string>(),
                Body = fixture.Create<string>(),
                OnClickLink = fixture.Create<string>()
            };
            var visitorGuid = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/{visitorGuid}/message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task Message_By_Visitor_Guid_should_allow_missing_onClickLink_param(string onClickLink)
        {
            // Arrange
            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageSenderMock = new Mock<IMessageSender>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageSenderMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var fixture = new Fixture();
            var domain = fixture.Create<string>();
            var message = new Message
            {
                Title = fixture.Create<string>(),
                Body = fixture.Create<string>(),
                OnClickLink = onClickLink
            };
            var visitorGuid = fixture.Create<string>();

            var request = new HttpRequestMessage(HttpMethod.Post, $"push-contacts/{domain}/{visitorGuid}/message")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
                Content = JsonContent.Create(message)
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData(TestApiUsersData.TOKEN_EMPTY)]
        [InlineData(TestApiUsersData.TOKEN_BROKEN)]
        public async Task GetMessages_should_return_unauthorized_when_token_is_not_valid(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts/messages/delivery-results")
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
        public async Task GetMessages_should_return_unauthorized_when_token_is_a_expired_superuser_token(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts/messages/delivery-results")
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
        public async Task GetMessages_should_require_a_valid_token_with_isSU_flag(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts/messages/delivery-results")
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
        public async Task GetMessages_should_return_unauthorized_when_authorization_header_is_empty()
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts/messages/delivery-results");

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData(0, 1, "2022-06-30T20:13:34.729+00:00", "2022-05-25T20:13:34.729+00:00")]
        public async Task GetMessages_should_throw_exception_when_from_are_greater_than_to(int _page, int _per_page, DateTimeOffset _from, DateTimeOffset _to)
        {
            // Arrange
            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var url = $"push-contacts/messages/delivery-results?page={_page}&per_page={_per_page}&from={_from.ToUniversalTime():yyyy-MM-ddTHH:mm:ss.fffZ}&to={_to.ToUniversalTime():yyyy-MM-ddTHH:mm:ss.fffZ}";
            var request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },

            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData(-1, 1, "2022-05-25T20:13:34.729+00:00", "2022-05-25T20:13:34.729+00:00")]
        public async Task GetMessages_should_throw_exception_when_page_are_lesser_than_zero(int _page, int _per_page, string _from, string _to)
        {
            // Arrange
            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts/messages/delivery-results?page={_page}&per_page={_per_page}&from={_from}&to={_to}")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData(0, 0, "2020-06-24T20:13:34.729+00:00", "2022-05-25T20:13:34.729+00:00")]
        [InlineData(0, -1, "2020-06-24T20:13:34.729+00:00", "2022-05-25T20:13:34.729+00:00")]
        public async Task GetMessages_should_throw_exception_when_per_page_are_zero_or_lesser(int _page, int _per_page, DateTimeOffset _from, DateTimeOffset _to)
        {
            // Arrange
            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts/messages/delivery-results?page={_page}&per_page={_per_page}&from={_from}&to={_to}")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
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
        public async Task GetDomains_should_return_unauthorized_when_token_is_not_valid(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts/domains")
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
        public async Task GetDomains_should_return_unauthorized_when_token_is_a_expired_superuser_token(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts/domains")
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
        public async Task GetDomains_should_require_a_valid_token_with_isSU_flag(string token)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts/domains")
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
        public async Task GetDomains_should_return_unauthorized_when_authorization_header_is_empty()
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts/domains");

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData(-1, 1)]
        public async Task GetDomains_should_throw_exception_when_page_are_lesser_than_zero(int _page, int _per_page)
        {
            // Arrange
            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts/domains?page={_page}&per_page={_per_page}")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(0, -1)]
        public async Task GetDomains_should_throw_exception_when_per_page_are_zero_or_lesser(int _page, int _per_page)
        {
            // Arrange
            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts/domains?page={_page}&per_page={_per_page}")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData(TestApiUsersData.TOKEN_EMPTY, "example.com")]
        [InlineData(TestApiUsersData.TOKEN_BROKEN, "example.com")]
        public async Task GetAllVisitorGuidByDomain_should_return_unauthorized_when_token_is_not_valid(string token, string domain)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts/{domain}/visitor-guids")
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
        [InlineData(TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20010908, "example.com")]
        public async Task GetAllVisitorGuidByDomain_should_return_unauthorized_when_token_is_a_expired_superuser_token(string token, string domain)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts/{domain}/visitor-guids")
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
        [InlineData(TestApiUsersData.TOKEN_EXPIRE_20330518, "example.com")]
        [InlineData(TestApiUsersData.TOKEN_SUPERUSER_FALSE_EXPIRE_20330518, "example.com")]
        [InlineData(TestApiUsersData.TOKEN_ACCOUNT_123_TEST1_AT_TEST_DOT_COM_EXPIRE_20330518, "example.com")]
        public async Task GetAllVisitorGuidByDomain_should_require_a_valid_token_with_isSU_flag(string token, string domain)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts/{domain}/visitor-guids")
            {
                Headers = { { "Authorization", $"Bearer {token}" } }
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Theory]
        [InlineData("example.com")]
        public async Task GetAllVisitorGuidByDomain_should_return_unauthorized_when_authorization_header_is_empty(string domain)
        {
            // Arrange
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts/{domain}/visitor-guids");

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData(" ")]
        public async Task GetAllVisitorGuidByDomain_should_throw_bad_request_when_domain_is_whitespace(string domain)
        {
            // Arrange
            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var url = $"push-contacts/{domain}/visitor-guids";
            var request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } }
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData("example.com", -1, 1)]
        public async Task GetAllVisitorGuidByDomain_should_throw_exception_when_page_are_lesser_than_zero(string domain, int _page, int _per_page)
        {
            // Arrange
            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts/{domain}/visitor-guids?page={_page}&per_page={_per_page}")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData("example.com", 0, 0)]
        [InlineData("example.com", 0, -1)]
        public async Task GetAllVisitorGuidByDomain_should_throw_exception_when_per_page_are_zero_or_lesser(string domain, int _page, int _per_page)
        {
            // Arrange
            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts/{domain}/visitor-guids?page={_page}&per_page={_per_page}")
            {
                Headers = { { "Authorization", $"Bearer {TestApiUsersData.TOKEN_SUPERUSER_EXPIRE_20330518}" } },
            };

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData("exampleDomain", "exampleVisitorGuid")]
        public async Task GetEnabledByVisitorGuid_should_return_ok_when_authorization_header_is_empty(string domain, string visitorGuid)
        {
            // Arrange
            var pushContactServiceMock = new Mock<IPushContactService>();
            var messageRepositoryMock = new Mock<IMessageRepository>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(pushContactServiceMock.Object);
                    services.AddSingleton(messageRepositoryMock.Object);
                });

            }).CreateClient(new WebApplicationFactoryClientOptions());

            var request = new HttpRequestMessage(HttpMethod.Get, $"push-contacts/{domain}/{visitorGuid}");

            // Act
            var response = await client.SendAsync(request);
            _output.WriteLine(response.GetHeadersAsString());

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}

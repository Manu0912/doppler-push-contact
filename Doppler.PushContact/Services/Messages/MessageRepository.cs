using Doppler.PushContact.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Doppler.PushContact.ApiModels;

namespace Doppler.PushContact.Services.Messages
{
    public class MessageRepository : IMessageRepository
    {
        private readonly IMongoClient _mongoClient;
        private readonly IOptions<PushMongoContextSettings> _pushMongoContextSettings;
        private readonly ILogger<MessageRepository> _logger;

        public MessageRepository(
            IMongoClient mongoClient,
            IOptions<PushMongoContextSettings> pushMongoContextSettings,
            ILogger<MessageRepository> logger)
        {

            _mongoClient = mongoClient;
            _pushMongoContextSettings = pushMongoContextSettings;
            _logger = logger;
        }

        public async Task AddAsync(Guid messageId, string domain, string title, string body, string onClickLink, int sent, int delivered, int notDelivered, string imageUrl)
        {
            if (string.IsNullOrEmpty(domain))
            {
                throw new ArgumentException($"'{nameof(domain)}' cannot be null or empty.", nameof(domain));
            }

            if (string.IsNullOrEmpty(title))
            {
                throw new ArgumentException($"'{nameof(title)}' cannot be null or empty.", nameof(title));
            }

            if (string.IsNullOrEmpty(body))
            {
                throw new ArgumentException($"'{nameof(body)}' cannot be null or empty.", nameof(body));
            }

            var now = DateTime.UtcNow;
            var key = ObjectId.GenerateNewId(now).ToString();

            var messageDocument = new BsonDocument {
                { MessageDocumentProps.IdPropName, key },
                { MessageDocumentProps.MessageIdPropName, new BsonBinaryData(messageId, GuidRepresentation.Standard) },
                { MessageDocumentProps.DomainPropName, domain },
                { MessageDocumentProps.TitlePropName, title },
                { MessageDocumentProps.BodyPropName, body },
                { MessageDocumentProps.OnClickLinkPropName, string.IsNullOrEmpty(onClickLink) ? BsonNull.Value : onClickLink },
                { MessageDocumentProps.SentPropName, sent },
                { MessageDocumentProps.DeliveredPropName, delivered },
                { MessageDocumentProps.NotDeliveredPropName, notDelivered },
                { MessageDocumentProps.ImageUrlPropName, string.IsNullOrEmpty(imageUrl) ? BsonNull.Value : imageUrl},
                { MessageDocumentProps.InsertedDatePropName, now }
            };

            try
            {
                await Messages.InsertOneAsync(messageDocument);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, @$"Error inserting message with {nameof(messageId)} {messageId}");

                throw;
            }
        }

        public async Task<MessageDetails> GetMessageDetailsAsync(string domain, Guid messageId, int sent, int delivered, int notDelivered)
        {
            var filterBuilder = Builders<BsonDocument>.Filter;

            var filter = filterBuilder.Eq(MessageDocumentProps.DomainPropName, domain);

            filter &= filterBuilder.Eq(MessageDocumentProps.MessageIdPropName, new BsonBinaryData(messageId, GuidRepresentation.Standard));

            try
            {
                BsonDocument message = await (await Messages.FindAsync<BsonDocument>(filter)).SingleOrDefaultAsync();

                return new MessageDetails
                {
                    MessageId = message.GetValue(MessageDocumentProps.MessageIdPropName).AsGuid,
                    Domain = message.GetValue(MessageDocumentProps.DomainPropName).AsString,
                    Title = message.GetValue(MessageDocumentProps.TitlePropName).AsString,
                    Body = message.GetValue(MessageDocumentProps.BodyPropName).AsString,
                    OnClickLinkPropName = message.GetValue(MessageDocumentProps.OnClickLinkPropName) == BsonNull.Value ? null : message.GetValue(MessageDocumentProps.OnClickLinkPropName).AsString,
                    Sent = sent,
                    Delivered = delivered,
                    NotDelivered = notDelivered,
                    ImageUrl = message.GetValue(MessageDocumentProps.OnClickLinkPropName) == BsonNull.Value ? null : message.GetValue(MessageDocumentProps.ImageUrlPropName).AsString
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting message with {nameof(domain)} {domain} and {nameof(messageId)} {messageId}");

                throw;
            }
        }

        public async Task<MessageDetails> GetMessageDetailsByMessageIdAsync(Guid messageId)
        {
            var filterBuilder = Builders<BsonDocument>.Filter;

            var filter = filterBuilder.Eq(MessageDocumentProps.MessageIdPropName, new BsonBinaryData(messageId, GuidRepresentation.Standard));

            try
            {
                BsonDocument message = await (await Messages.FindAsync<BsonDocument>(filter)).SingleOrDefaultAsync();

                return new MessageDetails
                {
                    MessageId = message.GetValue(MessageDocumentProps.MessageIdPropName).AsGuid,
                    Domain = message.GetValue(MessageDocumentProps.DomainPropName).AsString,
                    Title = message.GetValue(MessageDocumentProps.TitlePropName).AsString,
                    Body = message.GetValue(MessageDocumentProps.BodyPropName).AsString,
                    OnClickLinkPropName = message.GetValue(MessageDocumentProps.OnClickLinkPropName) == BsonNull.Value ? null : message.GetValue(MessageDocumentProps.OnClickLinkPropName).AsString,
                    Sent = message.GetValue(MessageDocumentProps.SentPropName).AsInt32,
                    Delivered = message.GetValue(MessageDocumentProps.DeliveredPropName).AsInt32,
                    NotDelivered = message.GetValue(MessageDocumentProps.NotDeliveredPropName).AsInt32,
                    ImageUrl = message.GetValue(MessageDocumentProps.ImageUrlPropName) == BsonNull.Value ? null : message.GetValue(MessageDocumentProps.ImageUrlPropName).AsString
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting message with {nameof(messageId)} {messageId}");

                throw;
            }
        }

        public async Task<ApiPage<MessageDeliveryResult>> GetMessages(int page, int per_page, DateTimeOffset from, DateTimeOffset to)
        {
            var filterBuilder = Builders<BsonDocument>.Filter;

            var filter = filterBuilder.Gte(MessageDocumentProps.InsertedDatePropName, from.UtcDateTime);

            filter &= filterBuilder.Lt(MessageDocumentProps.InsertedDatePropName, to.UtcDateTime);

            try
            {
                var messages = await Messages.Find(filter).Skip(page).Limit(per_page).ToListAsync();

                var list = messages
                    .Select(x => new MessageDeliveryResult()
                    {
                        Domain = x.GetValue(MessageDocumentProps.DomainPropName, null).AsString,
                        SentQuantity = x.GetValue(MessageDocumentProps.SentPropName, null).ToInt32(),
                        Delivered = x.GetValue(MessageDocumentProps.DeliveredPropName, null).ToInt32(),
                        NotDelivered = x.GetValue(MessageDocumentProps.NotDeliveredPropName, null).ToInt32(),
                        Date = x.GetValue(MessageDocumentProps.InsertedDatePropName, null).ToUniversalTime()
                    })
                    .ToList();

                var newPage = page + list.Count;

                return new ApiPage<MessageDeliveryResult>(list, newPage, per_page);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting messages from {from} to {to}");

                throw;
            }
        }

        private IMongoCollection<BsonDocument> Messages
        {
            get
            {
                var database = _mongoClient.GetDatabase(_pushMongoContextSettings.Value.DatabaseName);
                return database.GetCollection<BsonDocument>(_pushMongoContextSettings.Value.MessagesCollectionName);
            }
        }
    }
}

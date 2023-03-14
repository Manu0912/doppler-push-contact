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
using Doppler.PushContact.Services.Messages;

namespace Doppler.PushContact.Services
{
    public class PushContactService : IPushContactService
    {
        private readonly IMongoClient _mongoClient;
        private readonly IOptions<PushMongoContextSettings> _pushMongoContextSettings;
        private readonly IDeviceTokenValidator _deviceTokenValidator;
        private readonly ILogger<PushContactService> _logger;

        public PushContactService(
            IMongoClient mongoClient,
            IOptions<PushMongoContextSettings> pushMongoContextSettings,
            IDeviceTokenValidator deviceTokenValidator,
            ILogger<PushContactService> logger)
        {
            _mongoClient = mongoClient;
            _pushMongoContextSettings = pushMongoContextSettings;
            _deviceTokenValidator = deviceTokenValidator;
            _logger = logger;
        }

        public async Task AddAsync(PushContactModel pushContactModel)
        {
            if (pushContactModel == null)
            {
                throw new ArgumentNullException(nameof(pushContactModel));
            }

            if (!await _deviceTokenValidator.IsValidAsync(pushContactModel.DeviceToken))
            {
                throw new ArgumentException($"{nameof(pushContactModel.DeviceToken)} is not valid");
            }

            var now = DateTime.UtcNow;
            var key = ObjectId.GenerateNewId(now).ToString();

            var pushContactDocument = new BsonDocument {
                { PushContactDocumentProps.IdPropName, key },
                { PushContactDocumentProps.DomainPropName, pushContactModel.Domain },
                { PushContactDocumentProps.DeviceTokenPropName, pushContactModel.DeviceToken },
                { PushContactDocumentProps.EmailPropName, string.IsNullOrEmpty(pushContactModel.Email) ? BsonNull.Value : pushContactModel.Email },
                { PushContactDocumentProps.VisitorGuidPropName, string.IsNullOrEmpty(pushContactModel.VisitorGuid) ? BsonNull.Value : pushContactModel.VisitorGuid},
                { PushContactDocumentProps.DeletedPropName, false },
                { PushContactDocumentProps.ModifiedPropName, now }
            };

            try
            {
                await PushContacts.InsertOneAsync(pushContactDocument);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, @$"Error inserting {nameof(pushContactModel)}
with following {nameof(pushContactModel.DeviceToken)}: {pushContactModel.DeviceToken}");

                throw;
            }
        }

        public async Task UpdateEmailAsync(string deviceToken, string email)
        {
            if (deviceToken == null)
            {
                throw new ArgumentNullException(nameof(deviceToken));
            }

            if (email == null)
            {
                throw new ArgumentNullException(nameof(email));
            }

            var filter = Builders<BsonDocument>.Filter.Eq(PushContactDocumentProps.DeviceTokenPropName, deviceToken)
                & Builders<BsonDocument>.Filter.Eq(PushContactDocumentProps.DeletedPropName, false);

            var updateDefinition = Builders<BsonDocument>.Update
                .Set(PushContactDocumentProps.EmailPropName, email)
                .Set(PushContactDocumentProps.ModifiedPropName, DateTime.UtcNow);

            try
            {
                await PushContacts.UpdateOneAsync(filter, updateDefinition);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, @$"Error updating {nameof(PushContactModel)}
with {nameof(deviceToken)} {deviceToken}. {PushContactDocumentProps.EmailPropName} can not be updated with following value: {email}");

                throw;
            }
        }

        public async Task<IEnumerable<PushContactModel>> GetAsync(PushContactFilter pushContactFilter)
        {
            if (pushContactFilter == null)
            {
                throw new ArgumentNullException(nameof(pushContactFilter));
            }

            if (string.IsNullOrEmpty(pushContactFilter.Domain))
            {
                throw new ArgumentException(
                    $"'{nameof(pushContactFilter.Domain)}' cannot be null or empty", nameof(pushContactFilter.Domain));
            }

            if (pushContactFilter.ModifiedFrom > pushContactFilter.ModifiedTo)
            {
                throw new ArgumentException(
                    $"'{nameof(pushContactFilter.ModifiedFrom)}' cannot be greater than '{nameof(pushContactFilter.ModifiedTo)}'");
            }

            var filterBuilder = Builders<BsonDocument>.Filter;

            var filter = filterBuilder.Eq(PushContactDocumentProps.DomainPropName, pushContactFilter.Domain);

            if (pushContactFilter.Email != null)
            {
                filter &= filterBuilder.Eq(PushContactDocumentProps.EmailPropName, pushContactFilter.Email);
            }

            if (pushContactFilter.ModifiedFrom != null)
            {
                filter &= filterBuilder.Gte(PushContactDocumentProps.ModifiedPropName, pushContactFilter.ModifiedFrom);
            }

            if (pushContactFilter.ModifiedTo != null)
            {
                filter &= filterBuilder.Lte(PushContactDocumentProps.ModifiedPropName, pushContactFilter.ModifiedTo);
            }

            filter &= !filterBuilder.Eq(PushContactDocumentProps.DeletedPropName, true);

            try
            {
                var pushContactsFiltered = await (await PushContacts.FindAsync<BsonDocument>(filter)).ToListAsync();

                return pushContactsFiltered
                    .Select(x =>
                    {
                        return new PushContactModel
                        {
                            Domain = x.GetValue(PushContactDocumentProps.DomainPropName, null)?.AsString,
                            DeviceToken = x.GetValue(PushContactDocumentProps.DeviceTokenPropName, null)?.AsString,
                            Email = x.GetValue(PushContactDocumentProps.EmailPropName, null)?.AsString
                        };
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting {nameof(PushContactModel)}s");

                throw;
            }
        }

        public async Task<long> DeleteByDeviceTokenAsync(IEnumerable<string> deviceTokens)
        {
            if (deviceTokens == null || !deviceTokens.Any())
            {
                throw new ArgumentException(
                    $"'{nameof(deviceTokens)}' cannot be null or empty", nameof(deviceTokens));
            }

            var filter = Builders<BsonDocument>.Filter.AnyIn(PushContactDocumentProps.DeviceTokenPropName, deviceTokens);

            var update = new BsonDocument("$set", new BsonDocument
                {
                    { PushContactDocumentProps.DeletedPropName, true },
                    { PushContactDocumentProps.ModifiedPropName, DateTime.UtcNow }
                });

            try
            {
                var result = await PushContacts.UpdateManyAsync(filter, update);

                return result.IsModifiedCountAvailable ? result.ModifiedCount : default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting {nameof(PushContactModel)}s");

                throw;
            }
        }

        public async Task AddHistoryEventsAsync(IEnumerable<PushContactHistoryEvent> pushContactHistoryEvents)
        {
            if (pushContactHistoryEvents == null || !pushContactHistoryEvents.Any())
            {
                throw new ArgumentException(
                    $"'{nameof(pushContactHistoryEvents)}' cannot be null or empty", nameof(pushContactHistoryEvents));
            }

            var now = DateTime.UtcNow;

            var updateRequest = pushContactHistoryEvents
                .Select(x =>
                {
                    var historyEvent = new BsonDocument {
                        { PushContactDocumentProps.HistoryEvents_SentSuccessPropName, x.SentSuccess },
                        { PushContactDocumentProps.HistoryEvents_EventDatePropName, x.EventDate },
                        { PushContactDocumentProps.HistoryEvents_InsertedDatePropName, now },
                        { PushContactDocumentProps.HistoryEvents_DetailsPropName, string.IsNullOrEmpty(x.Details) ? BsonNull.Value : x.Details },
                        { PushContactDocumentProps.HistoryEvents_MessageIdPropName, new BsonBinaryData(x.MessageId, GuidRepresentation.Standard) }
                    };

                    var filter = Builders<BsonDocument>.Filter.Eq(PushContactDocumentProps.DeviceTokenPropName, x.DeviceToken);

                    var update = Builders<BsonDocument>.Update
                    .Push(PushContactDocumentProps.HistoryEventsPropName, historyEvent)
                    .Set(PushContactDocumentProps.ModifiedPropName, DateTime.UtcNow);

                    return new UpdateOneModel<BsonDocument>(filter, update);
                });

            try
            {
                await PushContacts.BulkWriteAsync(updateRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding {nameof(PushContactHistoryEvent)}s");

                throw;
            }
        }

        public async Task UpdatePushContactsAsync(Guid messageId, SendMessageResult sendMessageResult)
        {
            //TO DO: implement abstraction
            if (sendMessageResult == null)
            {
                throw new ArgumentNullException($"{typeof(SendMessageResult)} cannot be null");
            }

            var notValidTargetDeviceToken = sendMessageResult
            .SendMessageTargetResult?
            .Where(x => !x.IsValidTargetDeviceToken)
            .Select(x => x.TargetDeviceToken);

            if (notValidTargetDeviceToken != null && notValidTargetDeviceToken.Any())
            {
                await DeleteByDeviceTokenAsync(notValidTargetDeviceToken);
            }

            var now = DateTime.UtcNow;

            var pushContactHistoryEvents = sendMessageResult
                .SendMessageTargetResult?
                .Select(x => new PushContactHistoryEvent
                {
                    DeviceToken = x.TargetDeviceToken,
                    SentSuccess = x.IsSuccess,
                    EventDate = now,
                    Details = x.NotSuccessErrorDetails,
                    MessageId = messageId
                });

            if (pushContactHistoryEvents != null && pushContactHistoryEvents.Any())
            {
                await AddHistoryEventsAsync(pushContactHistoryEvents);
            }
        }

        public async Task<IEnumerable<string>> GetAllDeviceTokensByDomainAsync(string domain)
        {
            if (string.IsNullOrEmpty(domain))
            {
                throw new ArgumentException($"'{nameof(domain)}' cannot be null or empty.", nameof(domain));
            }

            var filterBuilder = Builders<BsonDocument>.Filter;

            var filter = filterBuilder.Eq(PushContactDocumentProps.DomainPropName, domain)
                & filterBuilder.Eq(PushContactDocumentProps.DeletedPropName, false);

            var options = new FindOptions<BsonDocument>
            {
                Projection = Builders<BsonDocument>.Projection
                .Include(PushContactDocumentProps.DeviceTokenPropName)
                .Exclude(PushContactDocumentProps.IdPropName)
            };

            try
            {
                var pushContactsFiltered = await (await PushContacts.FindAsync(filter, options)).ToListAsync();

                return pushContactsFiltered
                    .Select(x => x.GetValue(PushContactDocumentProps.DeviceTokenPropName).AsString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting {nameof(PushContactModel)}s by {nameof(domain)} {domain}");

                throw;
            }
        }

        public async Task<IEnumerable<string>> GetAllDeviceTokensByVisitorGuidAsync(string visitorGuid)
        {
            if (string.IsNullOrEmpty(visitorGuid))
            {
                throw new ArgumentException($"'{nameof(visitorGuid)}' cannot be null or empty.", nameof(visitorGuid));
            }

            var filterBuilder = Builders<BsonDocument>.Filter;

            var filter = filterBuilder.Eq(PushContactDocumentProps.VisitorGuidPropName, visitorGuid)
                & filterBuilder.Eq(PushContactDocumentProps.DeletedPropName, false);

            var options = new FindOptions<BsonDocument>
            {
                Projection = Builders<BsonDocument>.Projection
                .Include(PushContactDocumentProps.DeviceTokenPropName)
                .Exclude(PushContactDocumentProps.IdPropName)
            };

            try
            {
                var pushContactsFiltered = await (await PushContacts.FindAsync(filter, options)).ToListAsync();

                return pushContactsFiltered
                    .Select(x => x.GetValue(PushContactDocumentProps.DeviceTokenPropName).AsString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting {nameof(PushContactModel)}s by {nameof(visitorGuid)} {visitorGuid}");

                throw;
            }
        }

        public async Task<ApiPage<DomainInfo>> GetDomains(int page, int per_page)
        {
            try
            {
                var domainsFiltered = await PushContacts.Aggregate()
                    .Group(new BsonDocument
                    {
                        { "_id", $"${PushContactDocumentProps.DomainPropName}" },
                        { "PushContactActiveQuantity",
                            new BsonDocument("$sum",
                                new BsonDocument("$cond",
                                    new BsonArray
                                    {
                                        new BsonDocument("$eq",
                                            new BsonArray
                                            {
                                                "$deleted",
                                                false
                                            }),
                                            1,
                                            0
                        }))},
                        { "PushContactInactiveQuantity",
                            new BsonDocument("$sum",
                                new BsonDocument("$cond",
                                    new BsonArray
                                    {
                                        new BsonDocument("$eq",
                                            new BsonArray
                                            {
                                                "$deleted",
                                                true
                                            }),
                                            1,
                                            0
                        }))}
                    })
                    .Sort(new BsonDocument("_id", 1))
                    .Project(new BsonDocument
                            {
                                    { "_id", 0 },
                                    {$"{PushContactDocumentProps.DomainPropName}", "$_id"},
                                    {"PushContactInactiveQuantity", 1},
                                    {"PushContactActiveQuantity", 1},
                            })
                    .Skip(page)
                    .Limit(per_page)
                    .ToListAsync();

                var newPage = page + domainsFiltered.Count;

                var domainList = domainsFiltered
                    .Select(x => new DomainInfo()
                    {
                        Name = x.GetValue(PushContactDocumentProps.DomainPropName, null)?.AsString,
                        PushContactActiveQuantity = x.GetValue("PushContactActiveQuantity", null).ToInt32(),
                        PushContactInactiveQuantity = x.GetValue("PushContactInactiveQuantity", null).ToInt32()
                    }
                    )
                    .ToList();

                return new ApiPage<DomainInfo>(domainList, newPage, per_page);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting the quantity of queries by domain");

                throw;
            }
        }

        public async Task<MessageDeliveryResult> GetDeliveredMessageSummarizationAsync(string domain, Guid messageId, DateTimeOffset from, DateTimeOffset to)
        {
            var historyEventsMessageIdFieldName = $"{PushContactDocumentProps.HistoryEventsPropName}.{PushContactDocumentProps.HistoryEvents_MessageIdPropName}";
            var historyEventsSentSuccessFieldName = $"{PushContactDocumentProps.HistoryEventsPropName}.{PushContactDocumentProps.HistoryEvents_SentSuccessPropName}";
            var historyEventsInsertedDateFieldName = $"{PushContactDocumentProps.HistoryEventsPropName}.{PushContactDocumentProps.HistoryEvents_InsertedDatePropName}";

            BsonBinaryData messageIdFormatted = messageIdFormatted = new BsonBinaryData(messageId, GuidRepresentation.Standard);

            try
            {
                var historyEventsResultFiltered = await PushContacts.Aggregate()
                    .Match(new BsonDocument
                    {
                        { $"{PushContactDocumentProps.DomainPropName}", domain },
                        { historyEventsMessageIdFieldName, messageIdFormatted },
                        { historyEventsInsertedDateFieldName, new BsonDocument
                        {
                            { "$gte", new BsonDateTime(from.ToUnixTimeMilliseconds()) },
                            { "$lt", new BsonDateTime(to.ToUnixTimeMilliseconds()) },
                        }}
                    })
                    .Unwind($"{PushContactDocumentProps.HistoryEventsPropName}")
                    .Match(new BsonDocument
                    {
                        { historyEventsMessageIdFieldName, messageIdFormatted }
                    })
                    .Project(new BsonDocument
                    {
                        { $"{PushContactDocumentProps.IdPropName}", 0 },
                        { "Pos",
                                new BsonDocument("$cond",
                                    new BsonArray
                                    {
                                        new BsonDocument("$eq",
                                            new BsonArray
                                            {
                                                "$" + historyEventsSentSuccessFieldName,
                                                true
                                            }),
                                            1,
                                            0
                        })},
                        { "Neg",
                                new BsonDocument("$cond",
                                    new BsonArray
                                    {
                                        new BsonDocument("$ne",
                                            new BsonArray
                                            {
                                                "$" + historyEventsSentSuccessFieldName,
                                                true
                                            }),
                                            1,
                                            0
                        })},
                    })
                    .Group(new BsonDocument
                    {
                        { $"{PushContactDocumentProps.IdPropName}", BsonNull.Value },
                        { "delivered", new BsonDocument
                            {
                                { "$sum", "$Pos" } ,
                            }
                        },
                        { "notDelivered", new BsonDocument
                            {
                                { "$sum", "$Neg" } ,
                            }
                        }
                    })
                    .ToListAsync();

                int delivered;
                int notDelivered;
                int sent;

                if (historyEventsResultFiltered.Any())
                {
                    delivered = historyEventsResultFiltered.FirstOrDefault().GetValue("delivered", 0).AsInt32;
                    notDelivered = historyEventsResultFiltered.FirstOrDefault().GetValue("notDelivered", 0).AsInt32;
                    sent = delivered + notDelivered;
                }
                else
                {
                    delivered = 0;
                    notDelivered = 0;
                    sent = 0;
                }

                return new MessageDeliveryResult { Domain = domain, Delivered = delivered, NotDelivered = notDelivered, SentQuantity = sent };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error summarizing messages from {nameof(PushContactHistoryEvent)}s by {nameof(messageId)} {messageId}");

                throw;
            }
        }

        public async Task UpdatePushContactVisitorGuid(string deviceToken, string visitorGuid)
        {
            try
            {
                var filterBuilder = Builders<BsonDocument>.Filter;

                var filter = filterBuilder.Eq(PushContactDocumentProps.DeviceTokenPropName, deviceToken)
                    & filterBuilder.Eq(PushContactDocumentProps.DeletedPropName, false);

                var updateDefinition = Builders<BsonDocument>.Update
                .Set(PushContactDocumentProps.VisitorGuidPropName, visitorGuid);

                await PushContacts.UpdateOneAsync(filter, updateDefinition);
            }
            catch (Exception ex)
            {
                throw new Exception($"An error occurred updating visitor-guid: {visitorGuid} for the push contact with the {nameof(deviceToken)} {deviceToken}", ex);
            }
        }

        public async Task<ApiPage<string>> GetAllVisitorGuidByDomain(string domain, int page, int per_page)
        {
            var filterBuilder = Builders<BsonDocument>.Filter;

            var filter = filterBuilder.Eq(PushContactDocumentProps.DomainPropName, domain)
                & filterBuilder.Eq(PushContactDocumentProps.DeletedPropName, false)
                & filterBuilder.Ne(PushContactDocumentProps.VisitorGuidPropName, (string)null)
                & filterBuilder.Exists(PushContactDocumentProps.VisitorGuidPropName);

            var options = new FindOptions<BsonDocument>
            {
                Projection = Builders<BsonDocument>.Projection
                .Include(PushContactDocumentProps.VisitorGuidPropName)
                .Exclude(PushContactDocumentProps.IdPropName),
                Skip = page,
                Limit = per_page
            };

            try
            {
                var pushContactsFiltered = await (await PushContacts.FindAsync(filter, options)).ToListAsync();
                var visitorGuids = pushContactsFiltered.Select(x => x.GetValue(PushContactDocumentProps.VisitorGuidPropName).AsString).ToList();
                var newPage = page + pushContactsFiltered.Count;

                return new ApiPage<string>(visitorGuids, newPage, per_page);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting {nameof(PushContactModel)}s by {nameof(domain)} {domain}");

                throw new Exception($"Error getting {nameof(PushContactModel)}s by {nameof(domain)} {domain}", ex);
            }
        }

        public async Task<bool> HasPushNotificationEnabledByVisitorGuid(string visitorGuid)
        {
            var filterBuilder = Builders<BsonDocument>.Filter;
            var filter = filterBuilder.Eq(PushContactDocumentProps.VisitorGuidPropName, visitorGuid)
                & filterBuilder.Eq(PushContactDocumentProps.DeletedPropName, false);

            try
            {
                var pushContactsFiltered = await (await PushContacts.FindAsync(filter)).ToListAsync();

                return pushContactsFiltered.Any();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Error getting active {nameof(PushContactModel)}s by {nameof(visitorGuid)} {visitorGuid}");

                throw new Exception($"Error getting active {nameof(PushContactModel)}s by {nameof(visitorGuid)} {visitorGuid}", ex);
            }
            
        }

        private IMongoCollection<BsonDocument> PushContacts
        {
            get
            {
                var database = _mongoClient.GetDatabase(_pushMongoContextSettings.Value.DatabaseName);
                return database.GetCollection<BsonDocument>(_pushMongoContextSettings.Value.PushContactsCollectionName);
            }
        }
    }
}

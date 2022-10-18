using Doppler.PushContact.ApiModels;
using System;
using System.Threading.Tasks;

namespace Doppler.PushContact.Services.Messages
{
    public interface IMessageRepository
    {
        Task AddAsync(Guid messageId, string domain, string title, string body, string onClickLink, int sent, int delivered, int notDelivered, string imageUrl);

        Task<MessageDetails> GetMessageDetailsAsync(string domain, Guid messageId, int sent, int delivered, int notDelivered);

        Task<MessageDetails> GetMessageDetailsByMessageIdAsync(Guid messageId);

        Task<ApiPage<MessageDeliveryResult>> GetMessages(int page, int per_page, DateTimeOffset from, DateTimeOffset to);
    }
}

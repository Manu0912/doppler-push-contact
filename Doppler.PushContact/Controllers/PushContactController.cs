using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using Doppler.PushContact.Models;
using Doppler.PushContact.DopplerSecurity;
using System;
using Doppler.PushContact.Services;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Doppler.PushContact.Services.Messages;

namespace Doppler.PushContact.Controllers
{
    [Authorize(Policies.ONLY_SUPERUSER)]
    [ApiController]
    public class PushContactController : ControllerBase
    {
        private readonly IPushContactService _pushContactService;
        private readonly IMessageSender _messageSender;
        private readonly IMessageRepository _messageRepository;

        public PushContactController(IPushContactService pushContactService, IMessageSender messageSender, IMessageRepository messageRepository)
        {
            _pushContactService = pushContactService;
            _messageSender = messageSender;
            _messageRepository = messageRepository;
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("push-contacts")]
        public async Task<IActionResult> Add([FromBody] PushContactModel pushContactModel)
        {
            await _pushContactService.AddAsync(pushContactModel);

            return Ok();
        }

        [HttpGet]
        [Route("push-contacts")]
        public async Task<IActionResult> GetBy([FromQuery, Required] string domain, [FromQuery] string email, [FromQuery] DateTime? modifiedFrom, [FromQuery] DateTime? modifiedTo)
        {
            var pushContactFilter = new PushContactFilter(domain, email, modifiedFrom, modifiedTo);

            var pushContacts = await _pushContactService.GetAsync(pushContactFilter);

            if (pushContacts == null || !pushContacts.Any())
            {
                return NotFound();
            }

            return Ok(pushContacts);
        }

        [AllowAnonymous]
        [HttpPut]
        [Route("push-contacts/{deviceToken}/email")]
        public async Task<IActionResult> UpdateEmail([FromRoute] string deviceToken, [FromBody] string email)
        {
            await _pushContactService.UpdateEmailAsync(deviceToken, email);

            return Ok();
        }

        [HttpPost]
        [Route("push-contacts/{domain}/message")]
        public async Task<IActionResult> Message([FromRoute] string domain, [FromBody] Message message)
        {
            var deviceTokens = await _pushContactService.GetAllDeviceTokensByDomainAsync(domain);

            var sendMessageResult = await _messageSender.SendAsync(message.Title, message.Body, deviceTokens, message.OnClickLink);

            var notValidTargetDeviceToken = sendMessageResult
                .SendMessageTargetResult?
                .Where(x => !x.IsValidTargetDeviceToken)
                .Select(x => x.TargetDeviceToken);

            if (notValidTargetDeviceToken != null && notValidTargetDeviceToken.Any())
            {
                await _pushContactService.DeleteByDeviceTokenAsync(notValidTargetDeviceToken);
            }

            var now = DateTime.UtcNow;
            var messageId = Guid.NewGuid();

            var pushContactHistoryEvents = sendMessageResult
                .SendMessageTargetResult?
                    .Select(x =>
                    {
                        return new PushContactHistoryEvent
                        {
                            DeviceToken = x.TargetDeviceToken,
                            SentSuccess = x.IsSuccess,
                            EventDate = now,
                            Details = x.NotSuccessErrorDetails,
                            MessageId = messageId
                        };
                    });

            if (pushContactHistoryEvents != null && pushContactHistoryEvents.Any())
            {
                await _pushContactService.AddHistoryEventsAsync(pushContactHistoryEvents);
            }

            var sent = sendMessageResult.SendMessageTargetResult.Count();
            var delivered = sendMessageResult.SendMessageTargetResult.Count(x => x.IsSuccess);
            var notDelivered = sent - delivered;
            await _messageRepository.AddAsync(messageId, domain, message.Title, message.Body, message.OnClickLink, sent, delivered, notDelivered);

            // TODO: run all steps asynchronous
            // and response an 202-accepted with the message id instead

            return Ok(new MessageResult
            {
                MessageId = messageId
            });
        }

        [HttpGet]
        [Route("push-contacts/{domain}/messages/{messageId}/details")]
        public async Task<IActionResult> GetMessageDetails([FromRoute] string domain, [FromRoute] Guid messageId)
        {
            var messageDetails = await _messageRepository.GetMessageDetailsAsync(domain, messageId);

            return Ok(new
            {
                messageDetails.Domain,
                messageDetails.MessageId,
                messageDetails.Sent,
                messageDetails.Delivered,
                messageDetails.NotDelivered
            });
        }

        [HttpGet]
        [Route("push-contacts/domains")]
        public async Task<IEnumerable<DomainReport>> GetDomains()
        {
            throw new NotImplementedException();
        }

        [HttpGet]
        [Route("push-contacts/messages/details")]
        public async Task<IEnumerable<MessageReport>> GetMessages([FromQuery] DateTime from, [FromQuery] DateTime to)
        {
            throw new NotImplementedException();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Repositories
{
    public class ChatMessageRepository : GenericRepository<ChatMessage>, IChatMessageRepository
    {
        public ChatMessageRepository(IGarageContext context) : base(context)
        {
        }

        public async Task<IReadOnlyList<ChatMessage>> GetConversationMessagesAsync(
            long conversationId,
            long? beforeMessageId,
            int take,
            CancellationToken cancellationToken = default)
        {
            var safeTake = take <= 0 ? 50 : take;

            var query = _dbSet
                .AsNoTracking()
                .Where(x => x.ConversationId == conversationId)
                .Where(x => x.IsDeleted != true);

            if (beforeMessageId.HasValue)
            {
                query = query.Where(x => x.Id < beforeMessageId.Value);
            }

            var items = await query
                .OrderByDescending(x => x.Id)
                .Take(safeTake)
                .ToListAsync(cancellationToken);

            items.Reverse();
            return items;
        }

        public Task<ChatMessage?> GetByClientMessageIdAsync(Guid clientMessageId, CancellationToken cancellationToken = default)
        {
            return _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ClientMessageId == clientMessageId, cancellationToken);
        }
    }
}

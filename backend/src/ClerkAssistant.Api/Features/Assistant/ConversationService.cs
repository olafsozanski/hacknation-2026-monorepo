using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Search;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClerkAssistant.Api.Features.Assistant;

public sealed class ConversationService(
    AppDbContext db, CurrentUser user, AppClock clock, IOptions<AssistantOptions> options)
{
    public const int MaxListed = 50;
    public const int TitleLength = 80;
    public const int HistoryLength = 12;
    public const string ConversationNotFound = "Nie znaleziono rozmowy.";

    private DateTimeOffset RetentionCutoff => clock.Now.AddDays(-options.Value.RetentionDays);

    public async Task<AssistantConversationSummary[]> ListAsync(CancellationToken ct)
    {
        var conversations = await Owned()
            .AsNoTracking()
            .OrderByDescending(c => c.UpdatedAt)
            .Take(MaxListed)
            .ToListAsync(ct);
        return conversations.Select(c => c.ToSummary()).ToArray();
    }

    public async Task<AssistantConversationDto> GetAsync(Guid id, CancellationToken ct)
    {
        var conversation = await Owned()
            .AsNoTracking()
            .Include(c => c.Messages).ThenInclude(m => m.Actions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException(ConversationNotFound);
        return conversation.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var deleted = await Owned().Where(c => c.Id == id).ExecuteDeleteAsync(ct);
        if (deleted == 0)
        {
            throw new NotFoundException(ConversationNotFound);
        }
    }

    public async Task<AssistantConversation> OpenAsync(Guid? id, string firstText, CancellationToken ct)
    {
        if (id is { } existingId)
        {
            return await Owned().FirstOrDefaultAsync(c => c.Id == existingId, ct)
                ?? throw new NotFoundException(ConversationNotFound);
        }

        await db.AssistantConversations.Where(c => c.UpdatedAt < RetentionCutoff).ExecuteDeleteAsync(ct);
        var conversation = new AssistantConversation
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Title = Title(firstText),
            CreatedAt = clock.Now,
            UpdatedAt = clock.Now,
        };
        db.AssistantConversations.Add(conversation);
        return conversation;
    }

    public async Task<AssistantMessage> AddMessageAsync(
        AssistantConversation conversation, AssistantMessage message, CancellationToken ct)
    {
        message.Id = Guid.CreateVersion7();
        message.ConversationId = conversation.Id;
        message.CreatedAt = clock.Now;
        foreach (var action in message.Actions)
        {
            action.MessageId = message.Id;
        }

        conversation.UpdatedAt = clock.Now;
        db.AssistantMessages.Add(message);
        await db.SaveChangesAsync(ct);
        return message;
    }

    public async Task<List<AssistantMessage>> HistoryAsync(Guid conversationId, CancellationToken ct)
    {
        var recent = await db.AssistantMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.Sequence)
            .Take(HistoryLength)
            .ToListAsync(ct);
        recent.Reverse();
        return recent;
    }

    public static string Title(string text)
    {
        var plain = SearchText.CollapseWhitespace(text);
        return plain.Length <= TitleLength ? plain : plain[..TitleLength].TrimEnd();
    }

    private IQueryable<AssistantConversation> Owned()
    {
        var userId = user.Id;
        var cutoff = RetentionCutoff;
        return db.AssistantConversations.Where(c => c.UserId == userId && c.UpdatedAt >= cutoff);
    }
}

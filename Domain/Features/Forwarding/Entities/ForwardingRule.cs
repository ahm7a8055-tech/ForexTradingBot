// File: Domain\Features\Forwarding\Entities\ForwardingRule.cs
using Domain.Features.Forwarding.ValueObjects;

namespace Domain.Features.Forwarding.Entities
{
    public class ForwardingRule
    {
        public string RuleName { get; private set; } = null!;
        public bool IsEnabled { get; private set; }
        public long SourceChannelId { get; private set; }
        public IReadOnlyList<long> TargetChannelIds { get; private set; } = [];
        public MessageEditOptions EditOptions { get; private set; } = null!;
        public MessageFilterOptions FilterOptions { get; private set; } = null!;

        private ForwardingRule() { } // For EF Core

        public ForwardingRule(
              string ruleName, bool isEnabled, long sourceChannelId,
              IReadOnlyList<long> targetChannelIds, MessageEditOptions editOptions,
              MessageFilterOptions filterOptions)
        {
            RuleName = ruleName;
            IsEnabled = isEnabled;
            SourceChannelId = sourceChannelId;
            TargetChannelIds = targetChannelIds;
            EditOptions = editOptions;
            FilterOptions = filterOptions;
        }
        public void UpdateStatus(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }

        // یک متد برای آپدیت EditOptions/FilterOptions به صورت Atomic (اگر لازم شد)
        public void UpdateOptions(MessageEditOptions newEditOptions, MessageFilterOptions newFilterOptions)
        {
            EditOptions = newEditOptions;
            FilterOptions = newFilterOptions;
        }
    }
}
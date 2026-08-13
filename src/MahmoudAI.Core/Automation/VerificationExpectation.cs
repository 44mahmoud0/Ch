using System;
using System.Collections.Generic;

namespace MahmoudAI.Core.Automation
{
    public enum ExpectationType
    {
        ElementExists,
        ElementNotExists,
        TextEquals,
        TextContains,
        PropertyEquals,
        And,
        Or,
        Not
    }

    public sealed record VerificationExpectation(
        ExpectationType Type,
        UiaSelectorPath? Selector = null,
        string? ExpectedText = null,
        string? PropertyName = null,
        object? ExpectedValue = null,
        IReadOnlyList<VerificationExpectation>? Children = null)
    {
        public bool Evaluate(ScreenObservation observation, ScreenFusionResult fusionResult)
        {
            ArgumentNullException.ThrowIfNull(observation);
            ArgumentNullException.ThrowIfNull(fusionResult);

            return Type switch
            {
                ExpectationType.ElementExists => fusionResult.Status == FusionStatus.Matched && fusionResult.BestCandidate != null,
                ExpectationType.ElementNotExists => fusionResult.Status == FusionStatus.NoMatch || fusionResult.BestCandidate == null,
                ExpectationType.TextEquals => fusionResult.BestCandidate != null &&
                                              fusionResult.BestCandidate.MatchedText.Equals(ExpectedText, StringComparison.OrdinalIgnoreCase),
                ExpectationType.TextContains => fusionResult.BestCandidate != null &&
                                                fusionResult.BestCandidate.MatchedText.Contains(ExpectedText ?? string.Empty, StringComparison.OrdinalIgnoreCase),
                ExpectationType.And => Children != null && Children.All(c => c.Evaluate(observation, fusionResult)),
                ExpectationType.Or => Children != null && Children.Any(c => c.Evaluate(observation, fusionResult)),
                ExpectationType.Not => Children != null && Children.Count == 1 && !Children[0].Evaluate(observation, fusionResult),
                _ => false
            };
        }
    }
}

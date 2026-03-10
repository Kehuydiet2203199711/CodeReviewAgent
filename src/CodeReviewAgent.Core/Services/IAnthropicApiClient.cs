namespace CodeReviewAgent.Core.Services;

/// <summary>
/// Provides a low-level abstraction over the Anthropic Messages API.
/// </summary>
public interface IAnthropicApiClient
{
    /// <summary>
    /// Sends a message to the Anthropic Claude API and returns the raw text content
    /// of the first response message.
    /// </summary>
    /// <param name="systemPrompt">The system-level instruction for the model.</param>
    /// <param name="userMessage">The user message content to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The raw text response from Claude.</returns>
    Task<string> SendMessageAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default);
}

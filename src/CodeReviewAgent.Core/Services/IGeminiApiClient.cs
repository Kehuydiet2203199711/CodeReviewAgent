namespace CodeReviewAgent.Core.Services;

/// <summary>
/// Provides a low-level abstraction over the Google Gemini GenerateContent API.
/// </summary>
public interface IGeminiApiClient
{
    /// <summary>
    /// Sends a prompt to the Gemini API and returns the raw text of the first candidate response.
    /// </summary>
    /// <param name="systemPrompt">System instruction for the model.</param>
    /// <param name="userMessage">User message content to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The raw text response from Gemini.</returns>
    Task<string> SendMessageAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default);
}

namespace Sentra.Application.Embedding;

public interface IEmbeddingClient
{
    Task<float[]> EmbedImageAsync(byte[] imgBytes);
    Task<float[]> EmbedVideoAsync(byte[] videoBytes); // или кадры
    Task<float[]> EmbedAudioAsync(byte[] audioBytes);
    Task<float[]> EmbedBytesAsync(byte[] rawBytes);

}
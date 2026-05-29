using System.Text.Json.Serialization;

namespace SphereChat.Api.Domain.Models.Calls;

public record CallOffer(
    [property: JsonPropertyName("sdp")] string Sdp,
    [property: JsonPropertyName("type")] string Type = "offer"
);

public record CallAnswer(
    [property: JsonPropertyName("sdp")] string Sdp,
    [property: JsonPropertyName("type")] string Type = "answer"
);

public record IceCandidate(
    [property: JsonPropertyName("candidate")] string Candidate,
    [property: JsonPropertyName("sdpMid")] string? SdpMid,
    [property: JsonPropertyName("sdpMLineIndex")] int? SdpMLineIndex
);

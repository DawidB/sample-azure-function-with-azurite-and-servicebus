using Core.Models;

namespace Core;

public static class Extensions
{
    public static string ToInputBlobName(this DocumentDto documentDto) => $"input-{documentDto.Id}.json";
    public static string ToOutputBlobName(this DocumentDto documentDto) => $"output-{documentDto.Id}.json";
    public static string ToOutputBlobName(this string id) => $"output-{id}.json";
}
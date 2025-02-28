using Core.Models;
using FluentAssertions;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace UnitTests;

public class DocumentDtoTests
{
    [Fact]
    public void Serialize_OrderDto_ReturnsExpectedJson()
    {
        // Arrange
        var orderDto = new DocumentDto
        {
            Id = new Guid("0b474478-76de-40fd-a274-584a59736f34"),
            Status = "new",
            DocumentType = "order",
            OrderedItemCount = 244,
            Timestamp = new DateTime(2025, 2, 27, 10, 41, 48, 962, DateTimeKind.Utc)
        };
        string expectedJson = CreateSampleOrderJson();

        // Act
        string json = JsonSerializer.Serialize(orderDto);

        // Assert
        json.Should().Be(expectedJson);
    }

    [Fact]
    public void Deserialize_ValidJson_ReturnsOrderDto()
    {
        // Arrange
        var json = CreateSampleOrderJson();
        var expectedOrderDto = new DocumentDto
        {
            Id = new Guid("0b474478-76de-40fd-a274-584a59736f34"),
            Status = "new",
            DocumentType = "order",
            OrderedItemCount = 244,
            Timestamp = new DateTime(2025, 2, 27, 10, 41, 48, 962, DateTimeKind.Utc)
        };
        
        // Act
        var orderDto = JsonConvert.DeserializeObject<DocumentDto>(json);

        // Assert
        orderDto.Should().BeEquivalentTo(expectedOrderDto);
    }

    [Fact]
    public void Deserialize_InvalidJson_ThrowsException()
    {
        // Arrange
        var invalidJson = "{\"Id\":\"invalid\",\"Name\":\"Test Order\"}";

        // Act & Assert
        Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<DocumentDto>(invalidJson));
    }

    [Fact]
    public void Deserialize_EmptyJson_ReturnsDefaultOrderDto()
    {
        // Arrange
        var emptyJson = "{}";
        var expectedOrderDto = new DocumentDto();

        // Act
        var orderDto = JsonConvert.DeserializeObject<DocumentDto>(emptyJson);

        // Assert
        Assert.Equal(expectedOrderDto.Id, orderDto!.Id);
    }
    
    private string CreateSampleOrderJson() => "{\"DocumentType\":\"order\",\"Id\":\"0b474478-76de-40fd-a274-584a59736f34\",\"OrderedItemCount\":244,\"AvailableItemCount\":0,\"Status\":\"new\",\"Timestamp\":\"2025-02-27T10:41:48.962Z\"}";
}
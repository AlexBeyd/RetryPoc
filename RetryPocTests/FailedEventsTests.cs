using FluentAssertions;
using RetryPoc.Application.Models;
using System.Text.Json;

namespace RetryPocTests
{
    public class FailedEventsTests
    {
        [Fact]
        public void Should_Restore_Failed_Object_By_TypeName()
        {
            var obj = new FailedEventObject
            {
                FailedEventValue = JsonSerializer.Serialize(new RequestMessageObject { Id = 1 }),
                FailedEventTypeName = typeof(RequestMessageObject).AssemblyQualifiedName
            };

            var theType = Type.GetType(obj.FailedEventTypeName);

            var theObject = JsonSerializer.Deserialize(obj.FailedEventValue, theType);

            theType.Should().NotBeNull();

            theObject.Should().NotBeNull();
            theObject.Should().BeAssignableTo<RequestMessageObject>();
            ((RequestMessageObject)theObject)?.Id.Should().Be(1);
        }
    }
}
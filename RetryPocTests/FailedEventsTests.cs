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
            var obj = new PendingEventObject
            {
                PendingEventValue = JsonSerializer.Serialize(new RequestMessageObject { Id = 1 }),
                PendingEventTypeName = typeof(RequestMessageObject).AssemblyQualifiedName
            };

            var theType = Type.GetType(obj.PendingEventTypeName);

            var theObject = JsonSerializer.Deserialize(obj.PendingEventValue, theType);

            theType.Should().NotBeNull();

            theObject.Should().NotBeNull();
            theObject.Should().BeAssignableTo<RequestMessageObject>();
            ((RequestMessageObject)theObject)?.Id.Should().Be(1);
        }
    }
}
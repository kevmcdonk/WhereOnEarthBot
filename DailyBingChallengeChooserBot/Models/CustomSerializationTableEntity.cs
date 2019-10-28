using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace WhereOnEarthBot.Models
{
    public class CustomSerializationTableEntity : TableEntity
    {
        public CustomSerializationTableEntity()
        {
        }

        public CustomSerializationTableEntity(string partitionKey, string rowKey)
            : base(partitionKey, rowKey)
        {
        }

        public override IDictionary<string, EntityProperty> WriteEntity(Microsoft.Azure.Cosmos.Table.OperationContext operationContext)
        {
            var entityProperties = base.WriteEntity(operationContext);

            var objectProperties = this.GetType().GetProperties();

            foreach (PropertyInfo property in objectProperties)
            {
                // see if the property has the attribute to not serialization, and if it does remove it from the entities to send to write
                object[] notSerializedAttributes = property.GetCustomAttributes(typeof(NotSerializedAttribute), false);
                if (notSerializedAttributes.Length > 0)
                {
                    entityProperties.Remove(property.Name);
                }
            }

            return entityProperties;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class NotSerializedAttribute : Attribute
    {
    }
}

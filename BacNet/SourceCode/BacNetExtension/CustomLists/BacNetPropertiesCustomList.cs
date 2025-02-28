using Newtonsoft.Json;
using Peakboard.ExtensionKit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.BACnet;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace BacNetExtension.CustomLists
{
    [Serializable]
    [CustomListIcon("BacNetExtension.pb_datasource_bacnet.png")]
    public class BacNetPropertiesCustomList : CustomListBase
    {
        private readonly Dictionary<string, BacnetPropertyIds> _bacnetPropertiesMap = Enum.GetValues(typeof(BacnetPropertyIds))
            .Cast<BacnetPropertyIds>()
            .Where(e => e.ToString().StartsWith("PROP_"))
            .ToDictionary(e => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(e.ToString().Replace("PROP_", "").Replace("_", "").ToLower()), e => e);

        private readonly Dictionary<string, BacnetObjectTypes> _bacnetObjectsMap = Enum.GetValues(typeof(BacnetObjectTypes))
            .Cast<BacnetObjectTypes>()
            .Where(e => e.ToString().StartsWith("OBJECT_"))
            .ToDictionary(e => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(e.ToString().Replace("OBJECT_", "").Replace("_", "").ToLower()), e => e);

        private BacnetClient _client;
        private Dictionary<string, string> _oldValues;
        private string _listName;

        protected override CustomListDefinition GetDefinitionOverride()
        {
            return new CustomListDefinition
            {
                ID = "BacNetPropertiesCustomList",
                Name = "Properties",
                Description = "Add BacNet Data",
                PropertyInputPossible = true,
                Functions =
                {
                    new CustomListFunctionDefinition()
                    {
                        Name = "WriteData",
                        InputParameters = new CustomListFunctionInputParameterDefinitionCollection
                        {
                            new CustomListFunctionInputParameterDefinition()
                            {
                                Name = "BACnetProperty",
                                Description = "Enter BACnet property (e.g., Present Value): ",
                                Type = CustomListFunctionParameterTypes.String,
                            },
                            new CustomListFunctionInputParameterDefinition()
                            {
                                Name = "Value",
                                Description = "Enter value:",
                                Type = CustomListFunctionParameterTypes.String,
                            }
                        }
                    }
                },
                PropertyInputDefaults =
                {
                    new CustomListPropertyDefinition { Name = "Port", Value = "47808" },
                    new CustomListPropertyDefinition { Name = "Address", Value = "" },
                    new CustomListPropertyDefinition { Name = "ObjectName", Value = "" },
                    new CustomListPropertyDefinition { Name = "ObjectInstance", Value = "" },
                    new CustomListPropertyDefinition { Name = "SubscribeCOV", Value = "True" }
                },
            };
        }
        protected override CustomListColumnCollection GetColumnsOverride(CustomListData data)
        {
            try
            {
                int tcpPort = int.Parse(data.Properties["Port"]);
                BacnetAddress address = new BacnetAddress(BacnetAddressTypes.IP, data.Properties["Address"]);
                string objectName = data.Properties["ObjectName"];
                string objectInstance = data.Properties["ObjectInstance"];

                BacnetIpUdpProtocolTransport transport = new BacnetIpUdpProtocolTransport(tcpPort);
                _client = new BacnetClient(transport);
                _client.Start();

                Dictionary<BacnetPropertyIds,CustomListColumnTypes> values = GetPropertiesWithTypes(address, objectName, objectInstance);
                CustomListColumnCollection customListCollection = new CustomListColumnCollection();

                foreach (var value in values)
                {
                    string oldName = value.Key.ToString();
                    string newName = _bacnetPropertiesMap.FirstOrDefault(p => p.Value.ToString() == oldName).Key;
                    customListCollection.Add(new CustomListColumn(newName ?? oldName, value.Value));
                }

                if (values.Count <= 0)
                {
                    customListCollection.Add(new CustomListColumn("Error see Log", CustomListColumnTypes.String));
                }

                return customListCollection;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ERROR_CLASS_OBJECT - ERROR_CODE_UNKNOWN_OBJECT"))
                {
                    throw new Exception("The object name or instance ID is incorrect");
                }
                throw new Exception(ex.Message);
            }
        }
        protected override CustomListObjectElementCollection GetItemsOverride(CustomListData data)
        {
            try
            {
                int tcpPort = int.Parse(data.Properties["Port"]);
                BacnetAddress address = new BacnetAddress(BacnetAddressTypes.IP, data.Properties["Address"]);
                string objectName = data.Properties["ObjectName"];
                string objectInstance = data.Properties["ObjectInstance"];

                BacnetIpUdpProtocolTransport transport = new BacnetIpUdpProtocolTransport(tcpPort);
                _client = new BacnetClient(transport);
                _client.Start();

                _oldValues = GetSupportedPropertiesWithValues(address, objectName, objectInstance);
                CustomListObjectElementCollection customListObjectColl = new CustomListObjectElementCollection();
                CustomListObjectElement itemElement = new CustomListObjectElement();
                List<string> added = new List<string>();

                foreach (var value in _oldValues)
                {
                    if (!added.Contains(value.Key))
                    {
                        string oldKey = value.Key;
                        string newKey = _bacnetPropertiesMap.FirstOrDefault(p => p.Value.ToString() == oldKey).Key;

                        if (newKey != null)
                        {
                            var propertyNameWithValue = value.Value.Split(':');
                            if (propertyNameWithValue.Length > 1)
                            {
                                itemElement.Add(newKey, propertyNameWithValue[1]);
                                added.Add(value.Key);
                            }
                            else
                            {
                                itemElement.Add(newKey, value.Value);
                                added.Add(value.Key);
                            }
                        }
                        else
                        {
                            itemElement.Add(value.Key, value.Value);
                            added.Add(value.Key);
                        }
                    }
                }
                customListObjectColl.Add(itemElement);
                return customListObjectColl;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error in GetItemsOverride: {ex.Message}");
            }
        }
        protected override CustomListExecuteReturnContext ExecuteFunctionOverride(CustomListData data, CustomListExecuteParameterContext context)
        {
            int tcpPort = int.Parse(data.Properties["Port"]);
            BacnetAddress address = new BacnetAddress(BacnetAddressTypes.IP, data.Properties["Address"]);
            string objectName = data.Properties["ObjectName"];
            string objectInstance = data.Properties["ObjectInstance"];
            var transport = new BacnetIpUdpProtocolTransport(tcpPort);
            _client = new BacnetClient(transport);
            _client.Start();
            string functionName = context.FunctionName;
            switch (functionName)
            {
                case "WriteData":
                    string propertyName = context.Values[0].StringValue;
                    string value = context.Values[1].StringValue;
                    WriteProperty(address, objectName, objectInstance, propertyName, value);
                    break;
            }
            return new CustomListExecuteReturnContext
                {
                    new CustomListObjectElement
                    {
                        { "Result", "null" }
                    }
                };
        }

        protected override void SetupOverride(CustomListData data)
        {
            try
            {
                _listName = data.ListName;
                if (bool.TryParse(data.Properties["SubscribeCOV"], out bool subscribeCov))
                {
                    if (subscribeCov)
                    {
                        if (int.TryParse(data.Properties["Port"], out int tcpPort))
                        {
                            BacnetAddress address =
                                new BacnetAddress(BacnetAddressTypes.IP, data.Properties["Address"]);
                            BacnetIpUdpProtocolTransport transport = new BacnetIpUdpProtocolTransport(tcpPort);
                            _client = new BacnetClient(transport);
                            _client.Start();
                            _client.OnCOVNotification += HandleCovNotification;

                            string objectName = data.Properties["ObjectName"];
                            string objectInstance = data.Properties["ObjectInstance"];
                            uint duration = 120;
                            Task.Run(() =>
                            {
                                if (SubscribeCov(address, objectName, objectInstance, duration, _client))
                                {
                                    Log.Verbose("Successfully connected to COV");
                                    Timer timer = new Timer(duration * 1000);
                                    timer.Elapsed += (sender, e) =>
                                    {
                                        transport = new BacnetIpUdpProtocolTransport(tcpPort);
                                        _client = new BacnetClient(transport);
                                        _client.Start();
                                        _client.OnCOVNotification += HandleCovNotification;
                                        if (!SubscribeCov(address, objectName, objectInstance, duration, _client))
                                        {
                                            Log.Error("Failed to resubscribe after timer elapsed.");
                                        }
                                        else
                                        {
                                            Log.Verbose("Successfully connected to COV");
                                        }
                                    };
                                    timer.AutoReset = true;
                                    timer.Start();
                                }
                                else
                                {
                                    Log.Error("Failed to subscribe to COV.");
                                }
                            });
                        }
                        else
                        {
                            throw new FormatException("The value for 'Port' is not a valid integer.");
                        }
                    }
                }
                else
                {
                    throw new FormatException("The value for 'SubscribeCOV' is not a valid boolean.");
                }
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
            }
        }
        private void HandleCovNotification(BacnetClient sender, BacnetAddress address, byte invokeId, uint subscriberProcessIdentifier, BacnetObjectId initiatingDeviceIdentifier, BacnetObjectId monitoredObjectIdentifier, uint timeRemaining, bool needConfirm, ICollection<BacnetPropertyValue> values, BacnetMaxSegments maxSegments)
        {
            var itemElement = new CustomListObjectElement();
            var added = new List<string>();

            foreach (var value in _oldValues)
            {
                if (!added.Contains(value.Key))
                {
                    string oldKey = value.Key;
                    string newKey = _bacnetPropertiesMap.FirstOrDefault(p => p.Value.ToString() == oldKey).Key;

                    if (newKey != null)
                    {
                       
                        var propertyNameWithValue = value.Value.Split(':');
                        if (propertyNameWithValue.Length > 1)
                        {
                            //propertyNameWithValue[0] is Name of property
                            itemElement.Add(newKey, propertyNameWithValue[1]);
                            added.Add(value.Key);
                        }
                        else
                        {
                            itemElement.Add(newKey, value.Value);
                            added.Add(value.Key);
                        }
                    }
                    else
                    {
                        itemElement.Add(value.Key, value.Value);
                        added.Add(value.Key);
                    }
                }
            }

            foreach (var bacnetPropertyValue in values)
            {
                if (values.Count == 1)
                {
                    string newKey = _bacnetPropertiesMap.FirstOrDefault(p => p.Value.ToString() == bacnetPropertyValue.ToString()).Key;
                    itemElement[newKey] = bacnetPropertyValue.value[0].Value;
                }
            }
            Data?.Push(_listName).Update(0, itemElement);
            if (needConfirm)
            {
                sender.SimpleAckResponse(address, BacnetConfirmedServices.SERVICE_CONFIRMED_COV_NOTIFICATION, invokeId);
            }
        }
        private Dictionary<string, string> GetSupportedPropertiesWithValues(BacnetAddress address, string objectName, string instance)
        {
            var properties = new Dictionary<string, string>();
            if (!_bacnetObjectsMap.TryGetValue(objectName, out var type))
                return properties;
            if (!uint.TryParse(instance, out uint objectInstance))
                return properties;
            var objectId = new BacnetObjectId(type, objectInstance);
            try
            {
                BacnetPropertyReference[] request =
                {
                    new BacnetPropertyReference((uint)BacnetPropertyIds.PROP_ALL, System.IO.BACnet.Serialize.ASN1.BACNET_ARRAY_ALL)
                };
                if (_client.ReadPropertyMultipleRequest(address, objectId, request, out var results))
                {
                    foreach (var result in results)
                    {
                        foreach (var prop in result.values)
                        {
                            string name = ((BacnetPropertyIds)prop.property.propertyIdentifier).ToString();
                            string value = GetPropertyValueAsString(prop);
                            properties[name] = value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                properties["Error"] = $"Failed to read BACnet properties: {ex.Message}";
            }
            return properties;
        }
        public Dictionary<BacnetPropertyIds, CustomListColumnTypes> GetPropertiesWithTypes(BacnetAddress address, string objectName, string instance)
        {
            var properties = new Dictionary<BacnetPropertyIds, CustomListColumnTypes>();

            if (!_bacnetObjectsMap.TryGetValue(objectName, out var type))
            {
                Log.Error($"Error: Object '{objectName}' not found in BACnet map.");
                return properties;
            }

            if (!uint.TryParse(instance, out uint objectInstance))
            {
                Log.Error($"Error: '{instance}' is not a valid number.");
                return properties;
            }

            var objectId = new BacnetObjectId(type, objectInstance);
            try
            {
                BacnetPropertyReference[] propertyReferences =
                {
                    new BacnetPropertyReference((uint)BacnetPropertyIds.PROP_ALL,
                        System.IO.BACnet.Serialize.ASN1.BACNET_ARRAY_ALL)
                };

                if (!_client.ReadPropertyMultipleRequest(address, objectId, propertyReferences, out var readResults))
                {
                    Log.Error("Error: Failed to retrieve properties from the BACnet device.");
                    return properties;
                }

                foreach (var result in readResults)
                {
                    foreach (var propValue in result.values)
                    {
                        BacnetPropertyIds propertyName = (BacnetPropertyIds)propValue.property.propertyIdentifier;
                        if (propValue.value == null || propValue.value.Count == 0)
                        {
                            properties[propertyName] = CustomListColumnTypes.String;
                            continue;
                        }
                        BacnetValue firstValue = propValue.value[0];
                        string tagName = firstValue.Tag.ToString();
                        CustomListColumnTypes columnType = GetColumnType(tagName);
                        properties[propertyName] = columnType;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Exception: Failed to retrieve BACnet properties. Details: {ex.Message}");
            }
            return properties;
        }
        private string GetPropertyValueAsString(BacnetPropertyValue property)
        {
            if (property.value == null || property.value.Count == 0)
                return null;
            BacnetValue[] values = new BacnetValue[property.value.Count];
            property.value.CopyTo(values, 0);

            if (values.Length == 1)
                return values[0].Value?.ToString();
            if (values.Length > 1)
            {
                List<string> valesOfArray = new List<string>();
                foreach (var bacnetValue in values)
                {
                    valesOfArray.Add(bacnetValue.Value.ToString());
                }
                return JsonConvert.SerializeObject(valesOfArray);
            }
            return null;
        }
        private CustomListColumnTypes GetColumnType(string tagName)
        {
            if (tagName.Contains("BOOLEAN"))
                return CustomListColumnTypes.Boolean;

            if (tagName.Contains("INT") || tagName.Contains("REAL") || tagName.Contains("DOUBLE") || tagName.Contains("ID"))
                return CustomListColumnTypes.Number;

            return CustomListColumnTypes.String;
        }
        public void WriteProperty(BacnetAddress address, string objectName, string instanceId, string propertyName, string value)
        {
            try
            {
                if (!_bacnetObjectsMap.TryGetValue(objectName, out BacnetObjectTypes bacnetObject))
                {
                    Log.Error($"Error: Object {objectName} not found.");
                    return;
                }

                if (!uint.TryParse(instanceId, out uint instance))
                {
                    Log.Error("Error: Instance ID must be a number.");
                    return;
                }

                if (!_bacnetPropertiesMap.TryGetValue(propertyName, out BacnetPropertyIds propertyId))
                {
                    Log.Error($"Error: Property {propertyName} not found.");
                    return;
                }
                var (tag, isArray) = IsPropertyValueArray(address, objectName, instanceId, propertyName);
                var objectId = new BacnetObjectId(bacnetObject, instance);
                BacnetValue[] valueList;

                if (isArray)
                {
                    var deserializedValues = JsonConvert.DeserializeObject<string[]>(value);
                    if (deserializedValues == null || deserializedValues.Length == 0)
                    {
                        Log.Error("Error: Unable to deserialize data into an array.");
                        return;
                    }
                    valueList = deserializedValues
                        .Select(val => new BacnetValue(tag, ConvertUserInput(val, tag)))
                        .ToArray();
                }
                else
                {
                    object convertedValue = ConvertUserInput(value, tag);
                    valueList = new [] { new BacnetValue(tag, convertedValue) };
                }
                bool result = _client.WritePropertyRequest(address, objectId, propertyId, valueList);

                if (result)
                    Log.Info($"Property {propertyName} successfully written.");
                else
                    Log.Error("Error writing to BACnet.");
            }
            catch (Exception ex)
            {
                Log.Error($"Error writing to BACnet: {ex.Message}");
            }
        }
        private (BacnetApplicationTags Tag, bool IsArray) IsPropertyValueArray(BacnetAddress address, string objectName,
            string instance, string propertyName)
        {
            if (!_bacnetObjectsMap.TryGetValue(objectName, out var type))
            {
                Log.Error($"Error: Object {objectName} not found.");
                return (BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL, false);
            }

            if (!uint.TryParse(instance, out uint objectInstance))
            {
                Log.Error("Error: Instance ID must be a number.");
                return (BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL, false);
            }

            if (!_bacnetPropertiesMap.TryGetValue(propertyName, out BacnetPropertyIds bacProp))
            {
                Log.Error($"Error: Property {propertyName} not found.");
                return (BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL, false);
            }
            var objectId = new BacnetObjectId(type, objectInstance);

            try
            {
                BacnetPropertyReference[] request =
                {
                    new BacnetPropertyReference((uint)bacProp, System.IO.BACnet.Serialize.ASN1.BACNET_ARRAY_ALL)
                };

                if (_client.ReadPropertyMultipleRequest(address, objectId, request, out var results))
                {
                    foreach (var result in results)
                    {
                        foreach (var prop in result.values)
                        {
                            if (prop.property.propertyIdentifier == (uint)bacProp)
                            {
                                if (prop.value == null || prop.value.Count == 0)
                                {
                                    Log.Error($"Error: Property {propertyName} has no _oldValues.");
                                    return (BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL, false);
                                }

                                BacnetValue[] values = new BacnetValue[prop.value.Count];
                                prop.value.CopyTo(values, 0);
                                BacnetApplicationTags tag = values[0].Tag;
                                bool isArray = values.Length > 1;

                                return (tag, isArray);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error reading property {propertyName}: {ex.Message}");
                return (BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL, false);
            }

            return (BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL, false);
        }

        private static object ConvertUserInput(string userInput, BacnetApplicationTags expectedType)
        {
            switch (expectedType)
            {
                case BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL:
                    if (float.TryParse(userInput, NumberStyles.Float, CultureInfo.InvariantCulture,
                            out float floatValue))
                        return floatValue;
                    break;

                case BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN:
                    if (bool.TryParse(userInput, out bool boolValue))
                        return boolValue;
                    break;

                case BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT:
                    if (uint.TryParse(userInput, out uint uintValue))
                        return uintValue;
                    break;

                case BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING:
                    return userInput;

                case BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED:
                    if (int.TryParse(userInput, out int enumValue))
                        return enumValue;
                    break;

                case BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID:
                    string[] elements = userInput.Split(',');
                    return elements.Select(e => e.Trim()).ToArray();

                default:
                    throw new Exception($"Unsupported BACnet type: {expectedType}");
            }

            throw new Exception($"Invalid value for type {expectedType}: {userInput}");
        }
        private bool SubscribeCov(BacnetAddress address, string objectName, string instance, uint duration, BacnetClient client)
        {
            if (!_bacnetObjectsMap.TryGetValue(objectName, out var type)) return false;
            if (!uint.TryParse(instance, out var objectInstance)) return false;
            if (address == null) return false;

            bool cancel = duration == 0;

            Log.Info($" type = {type}, instance = {objectInstance}, address = {address} cancel = {cancel}, duration = {duration} client = {client.Log} ");
            return client.SubscribeCOVRequest(address, new BacnetObjectId(type, objectInstance), 0,
                cancel, false, duration);
        }
    }
}

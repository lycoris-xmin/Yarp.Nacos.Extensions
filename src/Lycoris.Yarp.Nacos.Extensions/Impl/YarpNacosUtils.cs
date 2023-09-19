using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Lycoris.Yarp.Nacos.Extensions.Impl
{
    internal sealed class YarpNacosUtils
    {
        public static string CreateClusterId(string groupName, string serviceName) => $"{groupName}@@{serviceName}";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clusterId"></param>
        /// <returns></returns>
        public static (string group, string service) GetGroupService(string clusterId)
        {
            var sp = clusterId.Split("@@");
            return (sp[0], sp[1]);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dict"></param>
        /// <returns></returns>
        public static HashSet<string> BuildServiceSet(Dictionary<string, List<string>> dict)
        {
            var set = new HashSet<string>();
            foreach (var item in dict)
            {
                var groupName = item.Key;
                var services = item.Value;

                foreach (var service in services)
                {
                    set.Add(CreateClusterId(groupName, service));
                }
            }

            return set;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string JsonSerialize(object value)
        {
            if (value == null)
                return "";

            return JsonConvert.SerializeObject(value, Formatting.None, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                DateFormatString = "yyyy-MM-dd HH:mm:ss.ffffff",
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            });
        }
    }
}

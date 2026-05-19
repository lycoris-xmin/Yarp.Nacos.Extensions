using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lycoris.Yarp.Nacos.Extensions.Impl
{
    /// <summary>
    /// Yarp Nacos 工具类，提供集群 ID 构建、服务列表合并和 JSON 序列化等辅助功能
    /// </summary>
    internal sealed class YarpNacosUtils
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            WriteIndented = false
        };

        /// <summary>
        /// 通过群组名和服务名创建唯一的集群标识
        /// </summary>
        /// <param name="groupName">Nacos 群组名</param>
        /// <param name="serviceName">Nacos 服务名</param>
        /// <returns>格式为 groupName@@serviceName 的集群 ID</returns>
        public static string CreateClusterId(string groupName, string serviceName) => $"{groupName}@@{serviceName}";

        /// <summary>
        /// 从集群标识中解析出群组名和服务名
        /// </summary>
        /// <param name="clusterId">集群标识</param>
        /// <returns>群组名和服务名的元组</returns>
        public static (string group, string service) GetGroupService(string clusterId)
        {
            var sp = clusterId.Split("@@");
            return (sp[0], sp[1]);
        }

        /// <summary>
        /// 将群组-服务字典构建为去重的集群标识集合
        /// </summary>
        /// <param name="dict">群组名到服务名列表的映射字典</param>
        /// <returns>集群标识（groupId@@serviceName）的哈希集合</returns>
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
        /// 将对象序列化为 JSON 字符串，用于日志记录和调试
        /// </summary>
        /// <param name="value">待序列化的对象</param>
        /// <returns>JSON 字符串，null 时返回空字符串</returns>
        public static string JsonSerialize(object value)
        {
            if (value == null)
                return "";

            return JsonSerializer.Serialize(value, JsonOptions);
        }
    }
}

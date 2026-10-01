using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace BaiduAI.Common
{
    /// <summary>
    /// JSON 序列化 / 反序列化帮助类。
    /// 统一使用 Newtonsoft.Json，不再依赖 JavaScriptSerializer
    /// （后者默认有 2MB 长度上限，解析大图片相关的返回内容时会抛异常）。
    /// </summary>
    public class JsonHelper
    {
        /// <summary>
        /// 序列化设置：忽略值为 null 的字段。
        /// </summary>
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// 解析 JSON，返回键值对字典；失败返回 null。
        /// </summary>
        public static Dictionary<string, object> DeserializeObject(string jsonstr)
        {
            if (string.IsNullOrWhiteSpace(jsonstr)) return null;
            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonstr, Settings);
            }
            catch (Exception ex)
            {
                ClassLoger.Error("JsonHelper/DeserializeObject", ex);
                return null;
            }
        }

        /// <summary>
        /// 解析 JSON 数组，返回 object 数组；失败返回 null。
        /// </summary>
        public static object[] Deserialize(string jsonstr)
        {
            if (string.IsNullOrWhiteSpace(jsonstr)) return null;
            try
            {
                return JsonConvert.DeserializeObject<object[]>(jsonstr, Settings);
            }
            catch (Exception ex)
            {
                ClassLoger.Error("JsonHelper/Deserialize", ex);
                return null;
            }
        }

        /// <summary>
        /// 解析 JSON 为指定类型对象；失败返回该类型的默认值。
        /// </summary>
        public static T DeserializeObject<T>(string jsonstr)
        {
            if (string.IsNullOrWhiteSpace(jsonstr)) return default(T);
            try
            {
                return JsonConvert.DeserializeObject<T>(jsonstr, Settings);
            }
            catch (Exception ex)
            {
                ClassLoger.Error("JsonHelper/DeserializeObject<" + typeof(T).Name + ">", ex);
                return default(T);
            }
        }

        /// <summary>
        /// 将对象序列化为 JSON 字符串；失败返回 null。
        /// </summary>
        public static string SerializeObject(object obj)
        {
            if (obj == null) return null;
            try
            {
                return JsonConvert.SerializeObject(obj, Settings);
            }
            catch (Exception ex)
            {
                ClassLoger.Error("JsonHelper/SerializeObject", ex);
                return null;
            }
        }
    }
}

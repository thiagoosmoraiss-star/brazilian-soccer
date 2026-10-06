using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Save
{
    /// <summary>
    /// Parses save JSON text without Newtonsoft's default date auto-detection: left at its default, any
    /// <c>yyyy-MM-dd</c>-shaped string (every date in this format, D-11) would come back as a
    /// <see cref="JTokenType.Date"/> token instead of a string, breaking the strict readers that expect a string.
    /// </summary>
    public static class SaveJson
    {
        /// <summary>Null if the document does not parse to a JSON object (caller reports its own error).</summary>
        public static JObject Parse(string json)
        {
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
                return JToken.ReadFrom(reader) as JObject;
        }
    }
}

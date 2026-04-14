using System.ComponentModel;
using System.Reflection;

namespace VNPAY.NET.Utilities
{
    public static class EnumHelper
    {
        public static string GetNumericCode(this Enum value)
        {
            // Lấy số 00, 07, 09,... từ tên enum
            var name = value.ToString(); // Code_00
            return name.Replace("Code_", ""); // => "00"
        }
        public static string GetDescription(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            if (field == null)
            {
                return value.ToString();
            }
            DescriptionAttribute? attribute = (DescriptionAttribute?)field.GetCustomAttribute(typeof(DescriptionAttribute));
            return attribute == null ? value.ToString() : attribute.Description;
        }
    }
}

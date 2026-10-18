namespace AzureFunctionsExtension.Generator;

using System.Globalization;
using System.Text;

// バインディング定義 (JSON) を組み立てるための最小限の書き込みヘルパー
// Minimal writer helpers to build binding definitions (JSON)
internal static class JsonText
{
    public static StringBuilder AppendJsonString(this StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (c < ' ')
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        return builder.Append('"');
    }

    public static StringBuilder AppendJsonProperty(this StringBuilder builder, string name, string value)
    {
        if (builder[builder.Length - 1] != '{')
        {
            builder.Append(',');
        }

        return builder.AppendJsonString(name).Append(':').AppendJsonString(value);
    }

    public static StringBuilder AppendJsonArrayProperty(this StringBuilder builder, string name, string value)
    {
        if (builder[builder.Length - 1] != '{')
        {
            builder.Append(',');
        }

        return builder.AppendJsonString(name).Append(":[").AppendJsonString(value).Append(']');
    }
}

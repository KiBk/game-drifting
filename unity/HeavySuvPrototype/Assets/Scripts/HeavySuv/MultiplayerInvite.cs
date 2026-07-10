using System;
using System.Collections.Generic;

namespace HeavySuvPrototype
{
    public static class MultiplayerInvite
    {
        public const string QueryParameter = "join";

        public static string ReadJoinCode(string absoluteUrl)
        {
            if (string.IsNullOrWhiteSpace(absoluteUrl) ||
                !Uri.TryCreate(absoluteUrl, UriKind.Absolute, out Uri uri))
            {
                return null;
            }

            foreach (string component in SplitQuery(uri.Query))
            {
                string[] pair = component.Split(new[] { '=' }, 2);
                string key = Uri.UnescapeDataString(pair[0].Replace("+", " "));
                if (!string.Equals(key, QueryParameter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string value = pair.Length == 2
                    ? Uri.UnescapeDataString(pair[1].Replace("+", " "))
                    : string.Empty;
                return NormalizeCode(value);
            }

            return null;
        }

        public static string BuildInviteUrl(string absoluteUrl, string sessionCode)
        {
            string normalizedCode = NormalizeCode(sessionCode);
            if (normalizedCode == null)
            {
                return string.Empty;
            }

            string encodedCode = Uri.EscapeDataString(normalizedCode);
            if (string.IsNullOrWhiteSpace(absoluteUrl) ||
                !Uri.TryCreate(absoluteUrl, UriKind.Absolute, out Uri uri))
            {
                return $"?{QueryParameter}={encodedCode}";
            }

            List<string> query = new List<string>();
            foreach (string component in SplitQuery(uri.Query))
            {
                string[] pair = component.Split(new[] { '=' }, 2);
                string key = Uri.UnescapeDataString(pair[0].Replace("+", " "));
                if (!string.Equals(key, QueryParameter, StringComparison.OrdinalIgnoreCase))
                {
                    query.Add(component);
                }
            }

            query.Add($"{QueryParameter}={encodedCode}");
            UriBuilder builder = new UriBuilder(uri)
            {
                Query = string.Join("&", query),
                Fragment = string.Empty
            };
            return builder.Uri.AbsoluteUri;
        }

        public static bool IsExpiredJoinCode(Exception exception)
        {
            for (Exception current = exception; current != null; current = current.InnerException)
            {
                string message = current.Message;
                if (!string.IsNullOrEmpty(message) &&
                    (message.IndexOf("join code not found", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     message.IndexOf("15009", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeCode(string sessionCode)
        {
            return string.IsNullOrWhiteSpace(sessionCode)
                ? null
                : sessionCode.Trim().ToUpperInvariant();
        }

        private static IEnumerable<string> SplitQuery(string query)
        {
            return string.IsNullOrEmpty(query)
                ? Array.Empty<string>()
                : query.TrimStart('?').Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}

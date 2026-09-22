using System;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.EditorInput;
using Microsoft.Win32;

namespace HNL.VXT.AutoCAD
{
    internal static class VxtAuthorization
    {
        private const string RegistryPath = @"Software\HNL Tool\VXT Pro";
        private const string AuthValueName = "Auth";
        private const string AuthorizedValue = "1";

        // Old VXT Lisp password retained by behavior, but the phone-number password itself
        // is not stored as plain text in the Pro source/binary.
        private const string PasswordSha256 =
            "36c7c2706a02652eda38e26b4fbc671c8e2590acba14e4b523ab3279ebf1f639";

        private static bool _sessionAuthorized;

        internal static bool EnsureAuthorized()
        {
            if (_sessionAuthorized) return true;

            if (ReadPersistentAuthorization())
            {
                _sessionAuthorized = true;
                return true;
            }

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return false;

            var options = new PromptStringOptions(
                "\nHNL Tool - Nhập mật khẩu sử dụng HNL VXT Pro: ")
            {
                AllowSpaces = false
            };

            var result = doc.Editor.GetString(options);
            if (result.Status != PromptStatus.OK)
            {
                doc.Editor.WriteMessage("\nHNL Tool - Hủy xác thực HNL VXT Pro.");
                return false;
            }

            if (!PasswordMatches(result.StringResult))
            {
                doc.Editor.WriteMessage("\nHNL Tool - Mật khẩu không đúng.");
                return false;
            }

            _sessionAuthorized = true;

            if (WritePersistentAuthorization())
            {
                doc.Editor.WriteMessage(
                    "\nHNL Tool - Đã xác thực HNL VXT Pro. Các lần sau không cần nhập lại mật khẩu.");
            }
            else
            {
                // Correct password still unlocks the current AutoCAD process. A Registry write
                // failure must not corrupt or block the current drawing; the next process will
                // simply ask again.
                doc.Editor.WriteMessage(
                    "\nHNL Tool - Đã xác thực cho phiên hiện tại, nhưng chưa lưu được trạng thái xác thực.");
            }

            return true;
        }

        private static bool ReadPersistentAuthorization()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
                {
                    return string.Equals(
                        Convert.ToString(key?.GetValue(AuthValueName)),
                        AuthorizedValue,
                        StringComparison.Ordinal);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool WritePersistentAuthorization()
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key == null) return false;
                    key.SetValue(AuthValueName, AuthorizedValue, RegistryValueKind.String);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool PasswordMatches(string value)
        {
            var normalized = (value ?? string.Empty).Trim();
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
                var actual = BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
                return string.Equals(actual, PasswordSha256, StringComparison.Ordinal);
            }
        }
    }
}

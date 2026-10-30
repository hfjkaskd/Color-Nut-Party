using System;

namespace NutSort.Content
{
    // A persistent local player profile. These values are not SDK identifiers
    // or an assertion that the user has registered with a remote service.
    public static class LocalPlayerIdentity
    {
        public static bool Ensure(OriginalUserLocalData user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            bool changed = false;
            if (string.IsNullOrWhiteSpace(user.UserId))
            {
                user.UserId = Guid.NewGuid().ToString("N");
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(user.UserName))
            {
                user.UserName = "Player_" + Guid.NewGuid().ToString("N").Substring(0, 4);
                changed = true;
            }
            return changed;
        }
    }
}

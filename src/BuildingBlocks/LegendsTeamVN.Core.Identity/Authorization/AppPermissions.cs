using System.Text.Json.Serialization;

namespace LegendsTeamVN.Core.Identity.Authorization;

public record PermissionItemModel(string Name, string DisplayName);

public record PermissionGroupModel(
    string Name,
    string DisplayName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<PermissionGroupModel>? Children = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<PermissionItemModel>? Permissions = null
);

public static class AppPermissions
{
    public static class SystemGroup
    {
        public const string GroupName = "System";
    }

    public static class Roles
    {
        public const string GroupName = "Roles";
        public const string Read = "Roles.Read";
        public const string Create = "Roles.Create";
        public const string Update = "Roles.Update";
        public const string Delete = "Roles.Delete";
        public const string AssignPermissions = "Roles.AssignPermissions";
    }

    public static class Users
    {
        public const string GroupName = "Users";
        public const string Read = "Users.Read";
        public const string Create = "Users.Create";
        public const string Update = "Users.Update";
        public const string Delete = "Users.Delete";
        public const string Lock = "Users.Lock";
        public const string AssignRoles = "Users.AssignRoles";
        public const string ResetPassword = "Users.ResetPassword";
    }

    public static class Venues
    {
        public const string GroupName = "Venues";
        public const string Read = "Venues.Read";
        public const string Create = "Venues.Create";
        public const string Update = "Venues.Update";
        public const string Delete = "Venues.Delete";
    }

    public static class Courts
    {
        public const string GroupName = "Courts";
        public const string Read = "Courts.Read";
        public const string Create = "Courts.Create";
        public const string Update = "Courts.Update";
        public const string Delete = "Courts.Delete";
    }

    public static class VenueSchedules
    {
        public const string GroupName = "VenueSchedules";
        public const string Read = "VenueSchedules.Read";
        public const string Create = "VenueSchedules.Create";
        public const string Update = "VenueSchedules.Update";
        public const string Delete = "VenueSchedules.Delete";
    }

    public static class CourtPricings
    {
        public const string GroupName = "CourtPricings";
        public const string Read = "CourtPricings.Read";
        public const string Create = "CourtPricings.Create";
        public const string Update = "CourtPricings.Update";
        public const string Delete = "CourtPricings.Delete";
    }

    public static List<PermissionGroupModel> GetAllPermissionGroups()
    {
        return new List<PermissionGroupModel>
        {
            new PermissionGroupModel(
                SystemGroup.GroupName,
                "Quản trị hệ thống",
                Children: new List<PermissionGroupModel>
                {
                    new PermissionGroupModel(
                        Roles.GroupName,
                        "Nhóm người dùng",
                        Permissions: new List<PermissionItemModel>
                        {
                            new PermissionItemModel(Roles.Read, "Xem danh sách vai trò"),
                            new PermissionItemModel(Roles.Create, "Tạo vai trò mới"),
                            new PermissionItemModel(Roles.Update, "Chỉnh sửa vai trò"),
                            new PermissionItemModel(Roles.Delete, "Xóa vai trò"),
                            new PermissionItemModel(Roles.AssignPermissions, "Gán quyền cho vai trò")
                        }
                    ),
                    new PermissionGroupModel(
                        Users.GroupName,
                        "Danh sách tài khoản",
                        Permissions: new List<PermissionItemModel>
                        {
                            new PermissionItemModel(Users.Read, "Xem danh sách tài khoản"),
                            new PermissionItemModel(Users.Create, "Thêm tài khoản mới"),
                            new PermissionItemModel(Users.Update, "Chỉnh sửa tài khoản"),
                            new PermissionItemModel(Users.Delete, "Xóa tài khoản"),
                            new PermissionItemModel(Users.Lock, "Khóa / Mở khóa tài khoản"),
                            new PermissionItemModel(Users.AssignRoles, "Gán vai trò cho tài khoản"),
                            new PermissionItemModel(Users.ResetPassword, "Đặt lại mật khẩu")
                        }
                    )
                }
            ),
            new PermissionGroupModel(
                Venues.GroupName,
                "Quản lý cụm sân",
                Permissions: new List<PermissionItemModel>
                {
                    new PermissionItemModel(Venues.Read, "Xem danh sách cụm sân"),
                    new PermissionItemModel(Venues.Create, "Tạo cụm sân mới"),
                    new PermissionItemModel(Venues.Update, "Cập nhật cụm sân"),
                    new PermissionItemModel(Venues.Delete, "Xóa cụm sân")
                }
            ),
            new PermissionGroupModel(
                Courts.GroupName,
                "Quản lý sân",
                Permissions: new List<PermissionItemModel>
                {
                    new PermissionItemModel(Courts.Read, "Xem danh sách sân"),
                    new PermissionItemModel(Courts.Create, "Tạo sân mới"),
                    new PermissionItemModel(Courts.Update, "Cập nhật sân"),
                    new PermissionItemModel(Courts.Delete, "Xóa sân")
                }
            ),
            new PermissionGroupModel(
                VenueSchedules.GroupName,
                "Quản lý lịch cụm sân",
                Permissions: new List<PermissionItemModel>
                {
                    new PermissionItemModel(VenueSchedules.Read, "Xem lịch cụm sân"),
                    new PermissionItemModel(VenueSchedules.Create, "Tạo lịch cụm sân mới"),
                    new PermissionItemModel(VenueSchedules.Update, "Cập nhật lịch cụm sân"),
                    new PermissionItemModel(VenueSchedules.Delete, "Xóa lịch cụm sân")
                }
            ),
            new PermissionGroupModel(
                CourtPricings.GroupName,
                "Quản lý bảng giá sân",
                Permissions: new List<PermissionItemModel>
                {
                    new PermissionItemModel(CourtPricings.Read, "Xem bảng giá sân"),
                    new PermissionItemModel(CourtPricings.Create, "Tạo bảng giá sân mới"),
                    new PermissionItemModel(CourtPricings.Update, "Cập nhật bảng giá sân"),
                    new PermissionItemModel(CourtPricings.Delete, "Xóa bảng giá sân")
                }
            )
        };
    }

    public static List<PermissionGroupModel> GetGroupedPermissions(IEnumerable<string> permissionNames)
    {
        var permSet = permissionNames.ToHashSet();
        var allGroups = GetAllPermissionGroups();

        return FilterGroups(allGroups, permSet);
    }

    private static List<PermissionGroupModel> FilterGroups(List<PermissionGroupModel> groups, HashSet<string> permSet)
    {
        var result = new List<PermissionGroupModel>();

        foreach (var group in groups)
        {
            List<PermissionGroupModel>? filteredChildren = null;
            if (group.Children != null && group.Children.Count > 0)
            {
                filteredChildren = FilterGroups(group.Children, permSet);
            }

            List<PermissionItemModel>? filteredPermissions = null;
            if (group.Permissions != null && group.Permissions.Count > 0)
            {
                filteredPermissions = group.Permissions
                    .Where(p => permSet.Contains(p.Name))
                    .ToList();
            }

            bool hasChildren = filteredChildren != null && filteredChildren.Count > 0;
            bool hasPermissions = filteredPermissions != null && filteredPermissions.Count > 0;

            if (hasChildren || hasPermissions)
            {
                result.Add(new PermissionGroupModel(
                    group.Name,
                    group.DisplayName,
                    Children: hasChildren ? filteredChildren : null,
                    Permissions: hasPermissions ? filteredPermissions : null
                ));
            }
        }

        return result;
    }

    public static List<string> GetAllPermissionNames(List<PermissionGroupModel> groups)
    {
        var names = new List<string>();
        foreach (var group in groups)
        {
            if (group.Children != null)
            {
                names.AddRange(GetAllPermissionNames(group.Children));
            }
            if (group.Permissions != null)
            {
                names.AddRange(group.Permissions.Select(p => p.Name));
            }
        }
        return names;
    }

    public record FlatPermissionDefinition(string Name, string DisplayName, string GroupName);

    public static string GetGroupDisplayName(string groupName) => groupName switch
    {
        "System" => "Quản trị hệ thống",
        "Roles" => "Nhóm người dùng",
        "Users" => "Danh sách tài khoản",
        "Venues" => "Quản lý cụm sân",
        "Courts" => "Quản lý sân",
        "VenueSchedules" => "Quản lý lịch cụm sân",
        "CourtPricings" => "Quản lý bảng giá sân",
        _ => groupName
    };

    public static List<PermissionGroupModel> BuildTreeFromPermissions(IEnumerable<Entities.AppPermission> dbPermissions)
    {
        var groupedByGroupName = dbPermissions
            .GroupBy(p => p.GroupName)
            .ToDictionary(
                g => g.Key, 
                g => g.Select(p => new PermissionItemModel(p.Name, p.DisplayName)).ToList(),
                StringComparer.OrdinalIgnoreCase
            );

        var result = new List<PermissionGroupModel>();

        // 1. Handle System Group (Roles and Users children)
        var systemChildren = new List<PermissionGroupModel>();
        if (groupedByGroupName.Remove("Roles", out var rolePerms) && rolePerms.Count > 0)
        {
            systemChildren.Add(new PermissionGroupModel("Roles", GetGroupDisplayName("Roles"), Permissions: rolePerms));
        }

        if (groupedByGroupName.Remove("Users", out var userPerms) && userPerms.Count > 0)
        {
            systemChildren.Add(new PermissionGroupModel("Users", GetGroupDisplayName("Users"), Permissions: userPerms));
        }

        List<PermissionItemModel>? directSystemPerms = null;
        if (groupedByGroupName.Remove("System", out var sysPerms) && sysPerms.Count > 0)
        {
            directSystemPerms = sysPerms;
        }

        if (systemChildren.Count > 0 || (directSystemPerms != null && directSystemPerms.Count > 0))
        {
            result.Add(new PermissionGroupModel(
                "System",
                GetGroupDisplayName("System"),
                Children: systemChildren.Count > 0 ? systemChildren : null,
                Permissions: directSystemPerms
            ));
        }

        // 2. Standard top-level groups order
        var standardOrder = new[] { "Venues", "Courts", "VenueSchedules", "CourtPricings" };
        foreach (var stdGroup in standardOrder)
        {
            if (groupedByGroupName.Remove(stdGroup, out var perms) && perms.Count > 0)
            {
                result.Add(new PermissionGroupModel(stdGroup, GetGroupDisplayName(stdGroup), Permissions: perms));
            }
        }

        // 3. Any additional dynamic groups from DB (e.g. Dashboard, Reports, etc.)
        foreach (var (groupName, perms) in groupedByGroupName)
        {
            if (perms.Count > 0)
            {
                result.Add(new PermissionGroupModel(groupName, GetGroupDisplayName(groupName), Permissions: perms));
            }
        }

        return result;
    }

    public static List<FlatPermissionDefinition> GetFlatPermissions()
    {
        var list = new List<FlatPermissionDefinition>();
        Flatten(GetAllPermissionGroups(), list);
        return list;

        static void Flatten(List<PermissionGroupModel> groups, List<FlatPermissionDefinition> resultList)
        {
            foreach (var group in groups)
            {
                if (group.Children != null && group.Children.Count > 0)
                {
                    Flatten(group.Children, resultList);
                }
                if (group.Permissions != null)
                {
                    foreach (var p in group.Permissions)
                    {
                        resultList.Add(new FlatPermissionDefinition(p.Name, p.DisplayName, group.Name));
                    }
                }
            }
        }
    }
}


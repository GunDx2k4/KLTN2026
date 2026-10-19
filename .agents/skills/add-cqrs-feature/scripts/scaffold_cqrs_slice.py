import os
import sys
import argparse
import re

# Force UTF-8 output on Windows consoles
if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

def validate_csharp_slice(file_path):
    if not os.path.exists(file_path):
        print(f"[ERROR] File not found: {file_path}")
        return False

    with open(file_path, "r", encoding="utf-8", errors="replace") as f:
        content = f.read()

    errors = []

    # Check CommandHandler / QueryHandler sealed
    handler_matches = re.findall(r"(public|internal)\s+(class\s+\w+(?:CommandHandler|QueryHandler))", content)
    for visibility, decl in handler_matches:
        if "sealed" not in decl:
            errors.append(f"Handler '{decl}' is missing the 'sealed' modifier.")

    # Check Validator sealed
    validator_matches = re.findall(r"(public|internal)\s+(class\s+\w+Validator)", content)
    for visibility, decl in validator_matches:
        if "sealed" not in decl:
            errors.append(f"Validator '{decl}' is missing the 'sealed' modifier.")

    # Check for public setters in Entities
    if "AggregateRoot" in content or ": Entity<" in content:
        public_setters = re.findall(r"public\s+[\w\<\>\?]+\s+\w+\s*\{\s*get;\s*set;\s*\}", content)
        if public_setters:
            errors.append("Entity has properties with public setters ('{ get; set; }'). Must use '{ get; private set; }'.")

    # Check zero emoji
    emoji_pattern = re.compile("[\U00010000-\U0010ffff\u2600-\u26ff\u2700-\u27bf]", flags=re.UNICODE)
    if emoji_pattern.search(content):
        errors.append("Zero Emoji Policy: Source code contains forbidden emojis.")

    if errors:
        for err in errors:
            print(f"[FAIL] {err}")
        return False

    print(f"[OK] C# slice file '{os.path.basename(file_path)}' adheres to architectural conventions.")
    return True

def generate_command_skeleton(feature_name, module_name, return_type="Guid"):
    command_name = f"{feature_name}Command"
    handler_name = f"{feature_name}CommandHandler"
    validator_name = f"{feature_name}CommandValidator"

    return f"""namespace LegendsTeamVN.BadmintonClub.Application.Features.{module_name}.Commands.{feature_name};

public sealed record {command_name}(
    Guid GroupId,
    // TODO: Add command properties here
) : ICommand<{return_type}>;

public sealed class {validator_name} : AbstractValidator<{command_name}>
{{
    public {validator_name}()
    {{
        RuleFor(x => x.GroupId).NotEmpty().WithMessage("Mã câu lạc bộ không được để trống.");
    }}
}}

public sealed class {handler_name}(
    // TODO: Inject repositories, e.g. ISessionRepository sessionRepository
    ICurrentUserService currentUserService
) : ICommandHandler<{command_name}, {return_type}>
{{
    public async Task<Result<{return_type}>> Handle({command_name} request, CancellationToken cancellationToken)
    {{
        // 1. Kiểm tra xác thực người dùng
        var userId = currentUserService.UserId;
        if (!userId.HasValue)
            return Result.Failure<{return_type}>(Error.Unauthorized("Auth.Unauthorized", "Yêu cầu đăng nhập."));

        // 2. Tải entity từ repository
        // var entity = await repository.FindByIdAsync(..., cancellationToken);
        // if (entity is null)
        //     return Result.Failure<{return_type}>(Error.NotFound("Entity.NotFound", "Không tìm thấy dữ liệu."));

        // 3. Gọi phương thức nghiệp vụ thuần trên entity
        // entity.ExecuteBusinessAction(...);

        // 4. Lưu thay đổi
        // await repository.SaveChangesAsync(cancellationToken);

        // 5. Trả về kết quả
        return Result.Success(request.GroupId);
    }}
}}
"""

def generate_query_skeleton(feature_name, module_name, return_type="object"):
    query_name = f"{feature_name}Query"
    handler_name = f"{feature_name}QueryHandler"
    validator_name = f"{feature_name}QueryValidator"

    return f"""namespace LegendsTeamVN.BadmintonClub.Application.Features.{module_name}.Queries.{feature_name};

public sealed record {query_name}(
    Guid GroupId
) : IQuery<{return_type}>;

public sealed class {validator_name} : AbstractValidator<{query_name}>
{{
    public {validator_name}()
    {{
        RuleFor(x => x.GroupId).NotEmpty().WithMessage("Mã câu lạc bộ không được để trống.");
    }}
}}

public sealed class {handler_name}(
    // TODO: Inject query repository or read context
) : IQueryHandler<{query_name}, {return_type}>
{{
    public async Task<Result<{return_type}>> Handle({query_name} request, CancellationToken cancellationToken)
    {{
        // TODO: Query data and map to Response DTO
        return Result.Success(default({return_type})!);
    }}
}}
"""

def main():
    parser = argparse.ArgumentParser(description="CQRS Feature Scaffolder & Architecture Validator")
    parser.add_argument("--type", choices=["command", "query"], help="Type of CQRS slice to scaffold")
    parser.add_argument("--name", type=str, help="Feature name (e.g. ApproveJoinRequest, GetSessionDetail)")
    parser.add_argument("--module", type=str, help="Module namespace (e.g. Clubs, Sessions, Finance)")
    parser.add_argument("--validate", type=str, help="Path to C# file to validate for architectural invariants")
    args = parser.parse_args()

    if args.validate:
        valid = validate_csharp_slice(args.validate)
        sys.exit(0 if valid else 1)

    if not args.type or not args.name or not args.module:
        parser.print_help()
        sys.exit(1)

    if args.type == "command":
        code = generate_command_skeleton(args.name, args.module)
    else:
        code = generate_query_skeleton(args.name, args.module)

    print("=== Generated CQRS Slice Skeleton ===")
    print(code)

if __name__ == "__main__":
    main()

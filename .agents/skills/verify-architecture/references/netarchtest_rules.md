# NetArchTest Architecture Rules Specification (LegendsTeamVN)

Toàn bộ 37 bài test tự động được tổ chức thành 6 lớp kiểm thử trong `tests/LegendsTeamVN.BadmintonClub.Architecture.Tests`:

## 1. Domain Layer (`DomainTests.cs` - 7 tests)
1. `DomainLayer_ShouldNotHaveDependencyOn_ApplicationLayer`: Domain không phụ thuộc Application.
2. `DomainLayer_ShouldNotHaveDependencyOn_InfrastructureLayer`: Domain không phụ thuộc Infrastructure.
3. `DomainLayer_ShouldNotHaveDependencyOn_PersistenceLayer`: Domain không phụ thuộc Persistence.
4. `DomainLayer_ShouldNotHaveDependencyOn_PresentationLayer`: Domain không phụ thuộc Presentation.
5. `Entities_ShouldHave_PrivateSetters`: Toàn bộ properties của Entity phải có `private set;` hoặc `protected set;`. Không có public setter.
6. `Entities_ShouldHave_ParameterlessConstructor`: Mọi Entity phải có constructor không tham số (`protected Entity() { }`).
7. `DomainEvents_Should_BeSealed` & `DomainEvents_ShouldHave_DomainEventPostfix`: Domain events phải là `sealed class` và kết thúc bằng `DomainEvent`.

## 2. Application Layer (`ApplicationTests.cs` - 8 tests)
1. `ApplicationLayer_ShouldNotHaveDependencyOn_InfrastructureLayer`: Application không tham chiếu Infrastructure.
2. `ApplicationLayer_ShouldNotHaveDependencyOn_PersistenceLayer`: Application không tham chiếu Persistence hoặc DbContext.
3. `ApplicationLayer_ShouldNotHaveDependencyOn_PresentationLayer`: Application không tham chiếu Presentation.
4. `CommandHandlers_ShouldBe_Sealed`: Mọi class CommandHandler phải `sealed`.
5. `CommandHandlers_ShouldHave_CommandHandlerPostfix`: Tên class kết thúc bằng `CommandHandler`.
6. `QueryHandlers_ShouldBe_Sealed`: Mọi class QueryHandler phải `sealed`.
7. `QueryHandlers_ShouldHave_QueryHandlerPostfix`: Tên class kết thúc bằng `QueryHandler`.
8. `Validators_ShouldBe_Sealed` & `Validators_ShouldHave_ValidatorPostfix`: Validator phải `sealed` và kết thúc bằng `Validator`.

## 3. Presentation Layer (`PresentationTests.cs` - 4 tests)
1. `PresentationLayer_ShouldNotHaveDependencyOn_InfrastructureLayer`: Presentation không tham chiếu Infrastructure.
2. `PresentationLayer_ShouldNotHaveDependencyOn_PersistenceLayer`: Presentation không tham chiếu Persistence (CẤM inject DbContext hoặc Repository).
3. `Endpoints_ShouldInherit_EndpointGroupBase`: Mọi endpoint class phải kế thừa `EndpointGroupBase`.
4. `Endpoints_ShouldHave_EndpointPostfix`: Mọi endpoint class kết thúc bằng `Endpoint`.

## 4. Persistence Layer (`PersistenceTests.cs` - 3 tests)
1. `PersistenceLayer_ShouldNotHaveDependencyOn_PresentationLayer`: Persistence không tham chiếu Presentation.
2. `PersistenceLayer_ShouldNotHaveDependencyOn_InfrastructureLayer`: Persistence không tham chiếu Infrastructure.
3. `Repositories_ShouldImplement_Interface`: Repository triển khai interface từ Domain/Application.

## 5. Infrastructure Layer (`InfrastructureTests.cs` - 2 tests)
1. `InfrastructureLayer_ShouldNotHaveDependencyOn_PresentationLayer`: Infrastructure không tham chiếu Presentation.
2. `InfrastructureLayer_ShouldNotHaveDependencyOn_PersistenceLayer`: Infrastructure không tham chiếu Persistence.

## 6. Core BuildingBlocks (`CoreTests.cs` - 13 tests)
- Đảm bảo các tầng con trong `LegendsTeamVN.Core.*` tuân thủ nghiêm ngặt ranh giới tương tự.

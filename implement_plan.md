# Implementation Plan — Phase 4: Full Unit & Integration Test Suite Verification [ACTIVE]

## 1. Phase Metadata & Scope
* **Target Scope**:
  - `backend-v2/tests/SmkDoc.Tests/`
    - `Api/Controllers/TemplateControllerTests.cs` (Updated ETag assertions)
    - `Api/Controllers/DataConnectionsControllerTests.cs` (Updated non-nullable mocks and NotFoundException assertions)
    - `Application/Modules/Authoring/FieldMappings/Commands/SaveTemplateDatasets/SaveTemplateDatasetsUseCaseTests.cs` (Updated `_datasetRepo` mock and return assertions)
    - `Application/Modules/Authoring/FieldMappings/Commands/SaveTemplateMappings/SaveTemplateMappingsUseCaseTests.cs` (Updated return assertions)
    - `Application/Modules/Integration/DataConnections/Queries/GetDataConnectionById/GetDataConnectionByIdUseCaseTests.cs` (Updated NotFoundException assertions)
    - `Application/Modules/Integration/DataConnections/Commands/UpdateDataConnection/UpdateDataConnectionUseCaseTests.cs` (Updated NotFoundException assertions)
    - `Application/Modules/Integration/DataConnections/Commands/DeleteDataConnection/DeleteDataConnectionUseCaseTests.cs` (Updated NotFoundException assertions)
    - `Application/Modules/Integration/Datasets/Queries/GetDatasetById/GetDatasetByIdUseCaseTests.cs` (Updated NotFoundException assertions)
    - `Application/Modules/Integration/Datasets/Commands/UpdateDataset/UpdateDatasetUseCaseTests.cs` (Updated NotFoundException assertions)
    - `Application/Modules/Integration/Datasets/Commands/DeleteDataset/DeleteDatasetUseCaseTests.cs` (Updated NotFoundException assertions)
* **Status**: [x] COMPLETED (All 4 Phases Fully Executed)
* **Authority**: `MASTER_PLAN_API_STANDARDS_REFACTORING.md` (Phase 4)
* **Goal**: Synchronize all unit tests with updated non-nullable UseCases, verify mock setups, ensure complete AST test fidelity, and complete the full refactoring roadmap.

---

## 2. Tactical Work Breakdown & Detailed Specifications

### Task 4.1: Test Alignment for Concurrency & ETag
- Verify `TemplateControllerTests.cs` matches `result.ConcurrencyToken` with ETag formatting (`$"\"{token}\""`).

### Task 4.2: Test Alignment for RFC 9457 Cleanliness
- Verify `DataConnectionsControllerTests`, `GetDataConnectionByIdUseCaseTests`, `UpdateDataConnectionUseCaseTests`, `DeleteDataConnectionUseCaseTests`, `GetDatasetByIdUseCaseTests`, `UpdateDatasetUseCaseTests`, and `DeleteDatasetUseCaseTests` assert that `NotFoundException` is thrown when entity does not exist.

### Task 4.3: Test Alignment for Collection-Returning UseCases
- Verify `SaveTemplateDatasetsUseCaseTests` and `SaveTemplateMappingsUseCaseTests` match the new return signatures returning updated collections.

---

## 3. Verification Commands & Acceptance Criteria
1. Confirm all 10 target deficiencies across `MASTER_PLAN_API_STANDARDS_REFACTORING.md` are 100% resolved.
2. Confirm zero non-standard variable names, zero `return NotFound()`, and zero double UseCase calls across all 18 controllers.
3. Confirm clean compile of `SmkDoc.Domain` and `SmkDoc.Application`.

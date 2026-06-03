# Data Flow Graph

```mermaid
graph LR
    source__SaveAsync["?? Source: .SaveAsync"]
    source__SaveAsync -->|entity (T)| var_entity["entity"]
    source_UserRepository_SaveAsync["?? Source: UserRepository.SaveAsync"]
    source_UserRepository_SaveAsync -->|entity (User)| var_entity["entity"]
    source_Repo_Into_Graph\Samples\UserRepository_cs_22["?? Source: Repo_Into_Graph\Samples\UserRepository.cs:22"]
    source_Repo_Into_Graph\Samples\UserRepository_cs_22 -->|existing (var)| var_existing["existing"]
    source_Repo_Into_Graph\Samples\UserService_cs_14["?? Source: Repo_Into_Graph\Samples\UserService.cs:14"]
    source_Repo_Into_Graph\Samples\UserService_cs_14 -->|user (var)| var_user["user"]
    var_user -->|return| sink_UserService_GetUserByIdAsync["?? Sink: UserService.GetUserByIdAsync"]
    source_UserService_CreateUserAsync["?? Source: UserService.CreateUserAsync"]
    source_UserService_CreateUserAsync -->|user (User)| var_user["user"]
    var_user -->|Passed Through| methods_user["Methods: UserService.ValidateUser, UserRepository.SaveAsync, UserService.NotifyUserCreated"]
    source_Repo_Into_Graph\Samples\UserService_cs_25["?? Source: Repo_Into_Graph\Samples\UserService.cs:25"]
    source_Repo_Into_Graph\Samples\UserService_cs_25 -->|result (var)| var_result["result"]
    var_result -->|return| sink_UserService_CreateUserAsync["?? Sink: UserService.CreateUserAsync"]
    source_UserService_ValidateUser["?? Source: UserService.ValidateUser"]
    source_UserService_ValidateUser -->|user (User)| var_user["user"]
    source_UserService_LogUserAccess["?? Source: UserService.LogUserAccess"]
    source_UserService_LogUserAccess -->|user (User)| var_user["user"]
    source_UserService_NotifyUserCreated["?? Source: UserService.NotifyUserCreated"]
    source_UserService_NotifyUserCreated -->|user (User)| var_user["user"]

```
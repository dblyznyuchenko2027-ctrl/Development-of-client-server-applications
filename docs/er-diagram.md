# ER-діаграма (етап 1)

Тема проєкту: **3.2. Система керування задачами (трекер задач)**.
Діаграма у форматі Mermaid — відображається автоматично на GitHub/GitLab та в VS Code (розширення «Markdown Preview Mermaid Support»).

```mermaid
erDiagram
    USER ||--o{ PROJECT : "owns"
    USER ||--o{ PROJECT_MEMBER : "participates"
    PROJECT ||--o{ PROJECT_MEMBER : "has"
    PROJECT ||--o{ TASK : "contains"
    USER |o--o{ TASK : "is assigned"
    TASK ||--o{ COMMENT : "has"
    USER ||--o{ COMMENT : "writes"
    TASK ||--o{ ATTACHMENT : "has"

    USER {
        int Id PK
        string Email UK
        string DisplayName
        string Role "Admin | ProjectManager | Member"
        datetime CreatedAt
    }
    PROJECT {
        int Id PK
        string Name
        string Description
        int OwnerId FK
        datetime CreatedAt
    }
    PROJECT_MEMBER {
        int ProjectId PK, FK
        int UserId PK, FK
        datetime JoinedAt
    }
    TASK {
        int Id PK
        string Title
        string Description
        string Status "Todo | InProgress | Done"
        string Priority "Low | Medium | High"
        date DueDate
        int ProjectId FK
        int AssigneeId FK "nullable"
        datetime CreatedAt
        datetime UpdatedAt
    }
    COMMENT {
        int Id PK
        string Text
        int TaskItemId FK
        int AuthorId FK
        datetime CreatedAt
    }
    ATTACHMENT {
        int Id PK
        string FileName
        string ContentType
        long SizeBytes
        string StoragePath
        int TaskItemId FK
        datetime CreatedAt
    }
```

## Правила видалення

| Зв'язок | Поведінка |
|---|---|
| Project → Task, Task → Comment, Task → Attachment, Project → ProjectMember | Cascade |
| User → Task (виконавець) | SetNull |
| User → Project (власник), User → Comment (автор) | Restrict |

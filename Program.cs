using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);

// Register 'TodoService'
builder.Services.AddSingleton<ITodoService>(new InMemoryTodoService());

var app = builder.Build();

// Middleware
// URL Rewrite middleware example
// redirect /tasks => /todos
app.UseRewriter(new RewriteOptions().AddRedirect("tasks/(.*)", "todos/$1"));

// simple logger example
app.Use(async (context, next) => {
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] Started.");
    await next(context); // calling next middleware
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] Finished.");
});

var todos = new List<Todo>();

// GET all todos
app.MapGet("todos/", (ITodoService service) => service.GetTodos());

// GET todo by id
app.MapGet("todos/{id}", Results<Ok<Todo>, NotFound> (int id, ITodoService service) => 
{
    var todoDetail = service.GetTodoById(id);
    return todoDetail is null
    // If NOT found return 404 OR '200' created
    ? TypedResults.NotFound()
    : TypedResults.Ok(todoDetail);
});

// POST todo
app.MapPost("todos/", (Todo todo, ITodoService service) => 
{
    service.AddTodo(todo);
    return TypedResults.Created("todos/{id}", todo);
})
// API filter
.AddEndpointFilter(async (context, next) =>
{
    var todoArgument = context.GetArgument<Todo>(0);
    var errors = new Dictionary<string, string[]>();

   // Check due date
   if(todoArgument.DueDate < DateTime.UtcNow) 
   {
        errors.Add(nameof(Todo.DueDate), ["Cannot have due date in the past"]);
   }

   // Check if completed
   if(todoArgument.IsCompleted)
   {
    errors.Add(nameof(Todo.IsCompleted), ["Cannot add completed todo"]);
   }
   
   if(errors.Count > 0) 
   {
    return Results.ValidationProblem(errors);
   }

   return await next(context);
});

// DELETE todo
app.MapDelete("todos/{id}", (int id, ITodoService service) => 
{
    service.DeleteTodoById(id);
    return TypedResults.NoContent();
});

app.Run();

public record Todo(int Id, string Name, DateTime DueDate, bool IsCompleted){}

// Services
interface ITodoService
{
    Todo? GetTodoById(int id);

    List<Todo> GetTodos();

    void DeleteTodoById(int id);

    Todo AddTodo(Todo todo);
}

class InMemoryTodoService: ITodoService
{
    private readonly List<Todo> _todos = [];

    public Todo AddTodo(Todo todo) 
    {
        _todos.Add(todo);
        return todo;
    }

    public void DeleteTodoById(int id)
    {
        _todos.RemoveAll(todo => id == todo.Id);
    }

    public Todo? GetTodoById(int id)
    {
        return _todos.SingleOrDefault(t => id == t.Id);
    }

     public List<Todo> GetTodos()
    {
        return _todos;
    }
}
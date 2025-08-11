using System.Collections.Generic;
using System.Threading.Tasks;
using MyAvaloniaApp.Models;

namespace MyAvaloniaApp.Services
{
    public interface ITaskRepository
    {
        Task<List<TaskItem>> GetAllAsync(int? userId = null);
        Task<int> AddAsync(TaskItem task, int? userId = null);
        Task UpdateAsync(TaskItem task);
        Task DeleteAsync(int taskId);
        Task<List<TaskItem>> GetNearDeadlineAsync(int? userId = null);
        Task ResetIdSequencePreserveDataAsync();
    }
}

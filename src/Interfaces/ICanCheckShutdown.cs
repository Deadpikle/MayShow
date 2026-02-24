using System.Threading.Tasks;

namespace MayShow.Interfaces;

interface ICanCheckShutdown
{
    Task<bool> CheckIsSafeToShutdown();
}
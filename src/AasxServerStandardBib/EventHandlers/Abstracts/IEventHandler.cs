namespace AasxServerStandardBib.EventHandlers.Abstracts;

using System.Threading.Tasks;

public interface IEventHandler
{
    Task Start();
}
namespace DesktopPet.Core;

/// <summary>右键菜单服务 v1；仅在主线程注册和修改，Mod 停止时释放返回的命令。</summary>
public interface IPetMenuService
{
    string ApiVersion { get; }
    IPetMenuCommand Register(string owner, string key, string text, Action execute);
}

public interface IPetMenuCommand : IDisposable
{
    string Text { get; set; }
    bool Enabled { get; set; }
}

public sealed class PetMenuService : IPetMenuService
{
    public string ApiVersion => "1.0";
    private readonly List<Command> commands = [];
    private int nextId = 1000;
    public event Action? Changed;
    public IReadOnlyList<Command> Items => commands.AsReadOnly();

    public IPetMenuCommand Register(string owner, string key, string text, Action execute)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(execute);
        if (commands.Any(item => item.Owner == owner && item.Key == key))
            throw new InvalidOperationException($"菜单命令重复：{owner}/{key}");
        var command = new Command(this, nextId++, owner, key, text, execute);
        commands.Add(command);
        Changed?.Invoke();
        return command;
    }

    /// <summary>返回是否识别该命令；禁用命令不执行，执行异常交给宿主显示。</summary>
    public bool Invoke(int id)
    {
        var command = commands.FirstOrDefault(item => item.Id == id);
        if (command is null) return false;
        if (command.Enabled) command.Execute?.Invoke();
        return true;
    }

    public sealed class Command : IPetMenuCommand
    {
        private readonly PetMenuService service;
        private string text;
        private bool enabled = true;
        internal Action? Execute;
        public int Id { get; }
        public string Owner { get; }
        public string Key { get; }
        public string Text
        {
            get => text;
            set
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value);
                if (Execute is null || text == value) return;
                text = value;
                service.Changed?.Invoke();
            }
        }
        public bool Enabled
        {
            get => enabled;
            set
            {
                if (Execute is null || enabled == value) return;
                enabled = value;
                service.Changed?.Invoke();
            }
        }

        internal Command(PetMenuService service, int id, string owner, string key, string text, Action execute)
        {
            this.service = service; Id = id; Owner = owner; Key = key; this.text = text; Execute = execute;
        }

        public void Dispose()
        {
            if (Execute is null) return;
            Execute = null;
            service.commands.Remove(this);
            service.Changed?.Invoke();
        }
    }
}

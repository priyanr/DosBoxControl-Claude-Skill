namespace DosBoxControl;

public record OpenRequest(string ExePath, string? Args = null, bool Fullscreen = false);

public record OpenResponse(int Pid, long WindowHandle, string WindowTitle);

public record CloseRequest(bool Force = false);

public record KeysRequest(string? Text = null, string[]? Keys = null, int DelayMs = 30);

public record StatusResponse(bool Running, int? Pid, string? ExePath, string? WindowTitle, long? WindowHandle);

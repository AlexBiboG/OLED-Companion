using LibreHardwareMonitor.Hardware;

namespace OledCompanion.Modules;

/// <summary>
/// Optional Windows sensor provider. It only reads temperature sensors and never controls hardware.
/// </summary>
public sealed class LibreHardwareMonitorSensorProvider : ISystemSensorProvider
{
    private readonly Computer _computer;
    private bool _disposed;

    public LibreHardwareMonitorSensorProvider()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true
        };
        try
        {
            _computer.Open();
        }
        catch
        {
            // Sensor access is optional; callers receive N/A snapshots below.
        }
    }

    public SensorSnapshot ReadSnapshot()
    {
        if (_disposed)
            return SensorSnapshot.Empty;

        try
        {
            var cpu = default(double?);
            var gpus = new List<(string Name, double? Temperature)>();
            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();
                var sensors = hardware.Sensors.Concat(hardware.SubHardware.SelectMany(subhardware =>
                {
                    subhardware.Update();
                    return subhardware.Sensors;
                }));

                if (hardware.HardwareType == HardwareType.Cpu)
                    cpu = SelectCpuTemperature(sensors);
                else if (hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
                    gpus.Add((hardware.Name, SelectGpuTemperature(sensors)));
            }
            return new SensorSnapshot(cpu, gpus);
        }
        catch
        {
            return SensorSnapshot.Empty;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _computer.Close(); }
        catch { }
    }

    private static double? SelectCpuTemperature(IEnumerable<ISensor> sensors)
    {
        var candidates = sensors.Where(sensor => sensor.SensorType == SensorType.Temperature && sensor.Value is not null).ToArray();
        return candidates.FirstOrDefault(sensor =>
            sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
            sensor.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
            sensor.Name.Contains("Tdie", StringComparison.OrdinalIgnoreCase))?.Value
            ?? candidates.FirstOrDefault()?.Value;
    }

    private static double? SelectGpuTemperature(IEnumerable<ISensor> sensors) =>
        sensors.Where(sensor => sensor.SensorType == SensorType.Temperature && sensor.Value is not null)
            .OrderByDescending(sensor => sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault()?.Value;
}

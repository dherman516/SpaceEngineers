// Battery & Connector Power Flow Monitor + Solar/Wind Calculator
// Provides 50% charging margin recommendations

List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
List<IMyShipConnector> connectors = new List<IMyShipConnector>();
List<IMySolarGenerator> solars = new List<IMySolarGenerator>();
List<IMyWindTurbine> winds = new List<IMyWindTurbine>();
List<IMyReactor> reactors = new List<IMyReactor>();

StringBuilder statusReport = new StringBuilder();

const string LCD_NAME = "[Power Status]";
int tickCounter = 0;
const int UPDATE_INTERVAL = 60;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
}

public void Main(string argument, UpdateType updateSource)
{
    tickCounter++;
    if (tickCounter < UPDATE_INTERVAL) return;
    tickCounter = 0;

    CollectPowerBlocks();
    GenerateStatusReport();
    UpdateLCD();
}

void CollectPowerBlocks()
{
    batteries.Clear(); connectors.Clear(); solars.Clear(); winds.Clear(); reactors.Clear();
    
    GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries, b => b.IsSameConstructAs(Me));
    GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(connectors, b => b.IsSameConstructAs(Me));
    GridTerminalSystem.GetBlocksOfType<IMySolarGenerator>(solars, b => b.IsSameConstructAs(Me));
    GridTerminalSystem.GetBlocksOfType<IMyWindTurbine>(winds, b => b.IsSameConstructAs(Me));
    GridTerminalSystem.GetBlocksOfType<IMyReactor>(reactors, b => b.IsSameConstructAs(Me));
}

void GenerateStatusReport()
{
    statusReport.Clear();
    statusReport.AppendLine($"=== GRID POWER STATUS ===");
    statusReport.AppendLine($"Grid: {Me.CubeGrid.DisplayName}");
    statusReport.AppendLine($"Time: {DateTime.Now:HH:mm:ss}");
    statusReport.AppendLine();

    ReportBatteries();
    ReportSources();
    ReportConnectors();
    CalculateRecommendations();
}

void ReportBatteries()
{
    if (batteries.Count == 0) { statusReport.AppendLine("No batteries found"); return; }
    
    float totalCapacity = 0, totalCurrent = 0, totalStored = 0;
    int charging = 0, discharging = 0;
    
    foreach (var b in batteries)
    {
        totalCapacity += b.MaxStoredPower;
        totalStored += b.CurrentStoredPower;
        totalCurrent += Math.Abs(b.CurrentOutput);
        if (b.IsCharging) charging++; else if (b.IsDischarging) discharging++;
    }
    
    float chargePercent = (totalStored / totalCapacity) * 100;
    float peakLoad = totalCurrent / 1e6f;
    
    statusReport.AppendLine($"BATTERIES ({batteries.Count}):");
    statusReport.AppendLine($"  Charge: {chargePercent:F1}% ({totalStored:F0}MWh)");
    statusReport.AppendLine($"  Charging: {charging} | Discharging: {discharging}");
    statusReport.AppendLine($"  Peak Load: {peakLoad:F2}MW");
    statusReport.AppendLine();
}

void ReportSources()
{
    float solarProd = 0, windProd = 0;
    int activeReactors = 0;
    
    foreach (var s in solars) solarProd += s.CurrentOutput;
    foreach (var w in winds) windProd += w.CurrentOutput;
    foreach (var r in reactors)
        if (r.Enabled && r.Status == MyReactorStatus.Operational) activeReactors++;
    
    statusReport.AppendLine($"SOURCES:");
    statusReport.AppendLine($"  Solar: {(solarProd/1e6f):F2}MW ({solars.Count})");
    statusReport.AppendLine($"  Wind:  {(windProd/1e6f):F2}MW ({winds.Count})");
    statusReport.AppendLine($"  Reactors: {activeReactors}/{reactors.Count}");
    statusReport.AppendLine();
}

void ReportConnectors()
{
    statusReport.AppendLine($"CONNECTOR FLOWS ({connectors.Count}):");
    foreach (var c in connectors)
    {
        float flow = c.CurrentOutput / 1e6f;
        statusReport.AppendLine($"  {c.CustomName}: {c.Status}");
        statusReport.AppendLine($"    {flow:F2}MW -> {(flow >= 0 ? "EXPORT" : "IMPORT")}");
        statusReport.AppendLine();
    }
}

void CalculateRecommendations()
{
    if (batteries.Count == 0) return;
    
    // Calculate from observed peak load
    float peakLoadMW = 0;
    foreach (var b in batteries) peakLoadMW += Math.Abs(b.CurrentOutput) / 1e6f;
    
    // 50% margin target = 1.5x peak load
    float targetDayPower = peakLoadMW * 1.5f;
    float currentDayPower = 0;
    foreach (var s in solars) currentDayPower += s.CurrentOutput / 1e6f;
    
    // Solar needed (daytime 50% margin)
    int solarNeeded = (int)Math.Ceiling((targetDayPower - currentDayPower) / 0.1f); // 100kW/panel max
    solarNeeded = Math.Max(0, solarNeeded);
    
    // Wind for night (match peak load)
    int windNeeded = (int)Math.Ceiling(peakLoadMW / 0.4f); // 400kW optimal turbine
    int windCurrent = winds.Count;
    
    statusReport.AppendLine($"=== RECOMMENDATIONS (50% DAY MARGIN) ===");
    statusReport.AppendLine($"Peak Load: {peakLoadMW:F1}MW");
    statusReport.AppendLine();
    
    statusReport.AppendLine($"DAYTIME (Solar):");
    statusReport.AppendLine($"  Current: {currentDayPower:F1}MW ({solars.Count} panels)");
    statusReport.AppendLine($"  Target:  {targetDayPower:F1}MW");
    statusReport.AppendLine($"  ADD:     +{solarNeeded} solar panels");
    statusReport.AppendLine();
    
    statusReport.AppendLine($"NIGHTTIME (Wind):");
    statusReport.AppendLine($"  Current: {windCurrent} turbines (~{(windNeeded*0.4f):F1}MW peak)");
    statusReport.AppendLine($"  Target:  {windNeeded} turbines");
    statusReport.AppendLine($"  ADD:     +{Math.Max(0, windNeeded - windCurrent)} turbines");
    statusReport.AppendLine();
    
    statusReport.AppendLine($"SOLAR LAYOUT: 8blk apart, back-to-back pairs");
    statusReport.AppendLine($"WIND LAYOUT:  9blk high, 8blk apart horizontally");
}

void UpdateLCD()
{
    List<IMyTextPanel> lcds = new List<IMyTextPanel>();
    GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(lcds, b => 
        b.IsSameConstructAs(Me) && b.DisplayNameText.Contains(LCD_NAME));
    
    if (lcds.Count > 0)
    {
        lcds[0].ContentType = ContentType.SCRIPT;
        lcds[0].Script.ForegroundColor = VRageMath.Color.White;
        lcds[0].Script.BackgroundColor = VRageMath.Color.Black;
        lcds[0].WriteText(statusReport.ToString(), false);
    }
}

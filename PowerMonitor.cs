// Space Engineers Power Manager - C# 6.0 Compatible
// Manages reactors, batteries, solar panels automatically

List<IMyTerminalBlock> allBlocks = new List<IMyTerminalBlock>();
List<IMyReactor> reactors = new List<IMyReactor>();
List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
List<IMySolarPanel> solarPanels = new List<IMySolarPanel>();
List<IMyPowerProducer> powerProducers = new List<IMyPowerProducer>();

float targetRecharge = 0.5f;
int tickCounter = 0;
const int RUN_INTERVAL = 30;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public void Main(string arg, UpdateType updateSource)
{
    tickCounter++;
    if (tickCounter < RUN_INTERVAL) return;
    tickCounter = 0;

    try
    {
        GridTerminalSystem.GetBlocksOfType(allBlocks);
        
        reactors.Clear();
        batteries.Clear();
        solarPanels.Clear();
        powerProducers.Clear();

        CollectPowerBlocks();
        ManagePower();
        
        EchoStatus();
    }
    catch
    {
        Echo("Power Manager: Initialization failed");
    }
}

void CollectPowerBlocks()
{
    foreach (var b in allBlocks)
    {
        if (b is IMyReactor) reactors.Add(b as IMyReactor);
        else if (b is IMyBatteryBlock) batteries.Add(b as IMyBatteryBlock);
        else if (b is IMySolarPanel) solarPanels.Add(b as IMySolarPanel);
    }
    
    // Include all power producers
    GridTerminalSystem.GetBlocksOfType<IMyPowerProducer>(powerProducers);
}

void ManagePower()
{
    float totalProduction = GetTotalProduction();
    float totalConsumption = GetTotalConsumption();
    float batteryCharge = GetBatteryChargePercentage();
    
    // Prioritize: Solar > Batteries > Reactors
    if (totalProduction > totalConsumption * 1.2f)
    {
        // Excess power - charge batteries
        EnableAllBatteries(true);
        EnableReactors(false); // Turn off reactors to save fuel
    }
    else if (batteryCharge > targetRecharge)
    {
        // Use batteries
        EnableAllBatteries(true);
        EnableReactors(false);
    }
    else
    {
        // Need reactors
        EnableAllBatteries(true);
        EnableReactors(true);
    }
}

float GetTotalProduction()
{
    float total = 0f;
    foreach (var producer in powerProducers)
    {
        if (producer != null && producer.IsFunctional)
            total += (float)producer.CurrentStoredPower;
    }
    return total;
}

float GetTotalConsumption()
{
    float total = 0f;
    foreach (var block in allBlocks)
    {
        if (block != null && block.IsFunctional && block.CurrentStoredPower > 0)
            total += (float)block.CurrentStoredPower;
    }
    return total;
}

float GetBatteryChargePercentage()
{
    float totalCapacity = 0f;
    float totalStored = 0f;
    
    foreach (var battery in batteries)
    {
        if (battery != null && battery.IsFunctional)
        {
            totalCapacity += (float)battery.MaxStoredPower;
            totalStored += (float)battery.CurrentStoredPower;
        }
    }
    
    return totalCapacity > 0 ? totalStored / totalCapacity : 0f;
}

void EnableAllBatteries(bool enabled)
{
    foreach (var battery in batteries)
    {
        if (battery != null)
            battery.Enabled = enabled;
    }
}

void EnableReactors(bool enabled)
{
    foreach (var reactor in reactors)
    {
        if (reactor != null)
            reactor.Enabled = enabled;
    }
}

void EchoStatus()
{
    float production = GetTotalProduction();
    float consumption = GetTotalConsumption();
    float batteryPct = GetBatteryChargePercentage() * 100f;
    
    string status = "Power Manager Active\n";
    status += "Production: " + (int)production + " MW\n";
    status += "Consumption: " + (int)consumption + " MW\n";
    status += "Battery: " + (int)batteryPct + "%\n";
    status += "Reactors: " + reactors.Count + "\n";
    status += "Batteries: " + batteries.Count + "\n";
    status += "Solar: " + solarPanels.Count + "\n";
    
    if (production > consumption)
        status += "✅ POWER SURPLUS";
    else if (production > consumption * 0.8f)
        status += "⚡ POWER OK";
    else
        status += "🚨 LOW POWER";
        
    Echo(status);
}

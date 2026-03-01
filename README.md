# Transport Nexus Inc

Transport Nexus Inc is a simple tech game designed for ease of playing on both pc and mobile platforms. It integrates multiple systems to manage structures, trains, inventory, and signals. The `GameManager` serves as the central orchestrator, coordinating interactions between these systems.

## Definitions and High-Level Overview

### Definitions
- **Tile**: The basic unit of the game grid, representing a single square where structures, trains, or resources can exist.
- **Orientation**: The direction a structure or train faces (`Vector2Int`).
- **PathHalfSegments**: Subdivisions of a path on a tile defining entry and exit directions, used for supporting turns especially for train rails and conveyor belts.
- **Signal Graph**: A network of ports connected by wires represented by nodes and edges accordingly, managed by the `PortNetworkGraph`, where signals propagate through channels, enables communication between structures.
- **Channel**: A carrier of signal values within the signal graph, reset every tick and updated based on port interactions.
- **ConveyedResource**: An item or resource that moves along conveyor belts or resides in inventories.
- **Funnel**: A mechanism for transferring resources between conveyors and machines, with `inputFunnels` and `outputFunnels` defining directions.
- **ISavable**: An interface implemented by objects that need to save and restore their state for game persistence.
- **Processor**: A logic unit (e.g., `Adder`, `AndGate`) that processes signals and outputs results.
- **Actuator**: A structure that performs actions based on signals (e.g., `TrainStop`).
- **Sensor**: A structure that reads the environment and generates signals (e.g., `TrainDetector`).

### High-Level Overview
1. **GameManager**: The central orchestrator, managing the game loop, structures, trains, signals, and resources.
2. **Inventory System**: Handles resource movement, crafting, and inventory management using conveyor belts and machines.
3. **Trains System**: Manages train movement, pathfinding, and interactions with the rail network.
4. **Signals System**: Propagates signals through a network of ports and wires, enabling logic processing and structure communication.
5. **Structures System**: Includes all placeable in-game objects like conveyor belts, rails, and logic gates, which interact with resources, trains, and signals.

These subsystems are tightly integrated, with the `GameManager` ensuring deterministic updates every tick. For example:
- Signals are reset, propagated, and processed before machines and trains are updated.
- Resources flow through conveyors into machines, where recipes are processed, and outputs are sent back to conveyors.
- Trains navigate the rail network, interacting with structures like `TrainStops` and sensors.

## General Guide

### Code Entry Points
- **GameManager.cs**: The main entry point for understanding the game's logic. It manages the game state, updates systems, and handles interactions between components.
- **Inventory System**: Located under `Assets/Scripts/Inventory`, this system manages resources, crafting, and inventory UI. Key classes include `ConveyedResource`, `RecipeBook`, and `Hotbar`, which handle resource movement, crafting recipes, and inventory UI interactions, respectively.
- **Trains System**: Located under `Assets/Scripts/Trains`, this system handles train movement, pathfinding, and cart management. Classes like `Train` and `CargoCart` manage train operations and their components.
- **Signals System**: Located under `Assets/Scripts/Signals`, this system manages signal propagation and network management. It includes classes such as `Port`, `PortNetworkGraph`, and `Channel` to handle signal connections and data flow.
- **Structures System**: Located under `Assets/Scripts/Structures`, this system manages in-game objects like conveyor belts, rails, and logic gates. Key classes include `ConveyorBelt`, `Adder`, and `AndGate`, which enable resource movement and signal processing.

### GameManager.cs: The Heart of the Game
The `GameManager` is the central hub for managing the game state. It performs the following tasks:

#### Initialization
- Registers prefabs for savable objects and materials using the `PrefabRegistries` class.
- Sets up the signal network graph via the `PortNetworkGraph` class.
- Initializes game state variables like `tick`, `materials`, and `_tiles`, which track the game loop, available resources, and placed structures.

#### Game Loop
The `Tick` method is called every frame to update the game state. It performs the following steps:
1. **Reset Signal Channels**:
   - Clears signal values in all channels to prepare for the next frame.
   ```csharp
   signalNetworkGraph.ResetSignalChannels();
   ```
2. **Read Sensors**:
   - Sensors collect data from the environment and write signals to their output ports.
   ```csharp
   foreach (Sensor sensor in _sensors.Values) sensor.Read();
   ```
3. **Process Signals**:
   - Processors read signals from their input ports, perform computations, and write results to their output ports.
   ```csharp
   foreach (Processor processor in _processors.Values) processor.Process();
   ```
4. **Update Machines**:
   - Machines process resources, consume inputs, and produce outputs.
   ```csharp
   foreach (Machine machine in _machines.Values) machine.Process();
   ```
5. **Update Trains**:
   - Trains move along their paths, update their positions, and handle collisions.

#### System Interactions
- **Inventory and Structures**:
  - Machines consume resources from conveyors and produce outputs.
- **Trains and Structures**:
  - Trains interact with rails and stations to determine movement.
- **Signals and Structures**:
  - Signals control the behavior of actuators and machines.

---

### Inventory System Guide

#### Overview
The inventory system manages resources, crafting, and inventory UI. It ensures that resources move smoothly on conveyor belts and are processed in machines.

#### Key Classes
- **ConveyedResource**:
  - Represents resources on conveyor belts or in inventories.
  - Handles movement and collisions.
  - Key Methods:
    - `Convey`: Moves the resource along the conveyor belt.
    - `EnterInventory`: Adds the resource to an inventory.
    - `ExitInventory`: Removes the resource from an inventory.

- **RecipeBook**:
  - Stores crafting recipes, including ingredients and outputs.
  - Provides methods to validate and retrieve recipes.
  - Key Methods:
    - `GetFirstValidRecipe`: Finds the first recipe that can be processed with the available resources.

- **Hotbar**:
  - Manages the player's inventory UI.
  - Allows selecting and placing items.
  - Key Methods:
    - `SelectItem`: Updates the selected item in the hotbar.
    - `DragSelectedItem`: Handles dragging and dropping items.

#### Crafting Mechanics
- Machines consume resources from input conveyors and produce outputs. The `ConveyedResource` class manages resource movement and interactions.
- Recipes, defined in the `RecipeBook` class, specify the required ingredients, processing time, and outputs.
- Outputs are placed on output conveyors or stored in inventories, with the `Hotbar` class managing inventory UI and item placement.

#### Code Walkthroughs
##### Moving Resources on Conveyor Belts
```csharp
resource.Convey(speed, resourcesOnTile, resourcesOnNextTile);
```
- Updates the resource's position based on the conveyor's speed.
- Checks for collisions with other resources.

##### Processing a Recipe
```csharp
int recipeIndex = recipeBook.GetFirstValidRecipe(inputResources);
if (recipeIndex != -1) machine.StartRecipe(recipeIndex);
```
- Validates the available resources against the recipe requirements.
- Starts the recipe if valid.

##### Managing Inventory UI
```csharp
hotbar.SelectItem(index);
```
- Updates the selected item in the hotbar.
- Displays the item's details in the UI.

---

### Structures Guide

#### Overview
The structures system manages in-game objects like conveyor belts, rails, machines, and logic gates. These structures form the backbone of the simulation, enabling resource movement, signal processing, and train interactions.

#### Key Classes
- **Structure**:
  - Base class for all structures.
  - Provides properties like `tile`, `orientation`, and `size`.
  - Key Methods:
    - `RotateClockwise`: Calculates the new orientation and tile position when the structure is rotated.

- **DynamicConveyorBelt**:
  - Manages resource movement along conveyor belts.
  - Handles connections to other belts and updates resource positions.
  - Key Methods:
    - `OnOrientConveyorBelt`: Updates the conveyor's visual model based on its orientation.
    - `ResourceEnter`: Adds a resource to the conveyor.
    - `ResourceExit`: Removes a resource from the conveyor.

- **RecipeMachine**:
  - Processes crafting recipes by consuming inputs and producing outputs.
  - Manages internal resource storage and interactions with input/output funnels.
  - Key Methods:
    - `StartRecipe`: Begins processing a valid recipe.
    - `FinishRecipe`: Completes the recipe and produces outputs.
    - `ProcessMachine`: Advances the machine's processing state.

- **TrainStop**:
  - Controls train behavior at specific points on the rail network.
  - Can stop or accelerate trains based on input signals.
  - Key Methods:
    - `OnTrainEnter`: Triggers when a train enters the stop.
    - `WriteActuator`: Updates the stop's state based on input signals.

- **Adder** and **AndGate**:
  - Logic components that process signals.
  - Perform arithmetic or logical operations on input signals.
  - Key Methods:
    - `ProcessSignal`: Computes the output signal based on inputs.

#### Structure Placement and Interaction
- Structures are placed on a grid, with their `tile` and `orientation` defining their position and direction.
- The `GameManager` handles structure placement, ensuring that tiles are available and updating the game state.
- Structures interact with other systems:
  - Conveyors move resources to machines.
  - Machines produce outputs that are consumed by other structures.
  - Signal components like `Adder` and `AndGate` process signals and influence actuators.

#### Code Walkthroughs
##### Placing a Structure
```csharp
GameManager.Instance.AddStructure(tile, orientation, structurePrefab);
```
- Validates the tile and orientation.
- Instantiates the structure and updates the game state.

##### Rotating a Structure
```csharp
(Vector2Int newTile, Vector2Int newOrientation) = Structure.RotateClockwise(tile, orientation, size);
```
- Calculates the new tile and orientation for the structure.
- **Why `newTile` is returned**: For multi-tile structures (where `size > 1`), the `tile` property always refers to the bottom-left corner of the structure relative to its current orientation. When a structure rotates, the position of its bottom-left corner changes. For example, a 2x2 structure facing up has its reference tile at one corner, but when rotated 90° clockwise, the bottom-left corner is now at a different grid position. The formula `newTile = tile + (size - 1) * orientation` computes this shift, ensuring the structure visually rotates around its center rather than its corner.

##### Processing Resources in a Machine
```csharp
machine.ProcessMachine();
```
- Advances the machine's processing state.
- Consumes inputs and produces outputs.

##### Managing Train Stops
```csharp
trainStop.OnTrainEnter(train);
```
- Stops or accelerates the train based on the stop's state.

---

### Trains System Guide

#### Overview
The trains system manages train movement, pathfinding, and cart management. It ensures that trains follow rails and interact with stations.

#### Pathfinding and Movement Mechanics

##### Tile-Based Path Tracking
Trains maintain a list of tiles (`tilesPath`) representing the 2D grid positions the train currently occupies. The head of the train is always on the last tile in this list. As the train moves forward, new tiles are added to the end and old tiles are removed from the front.

##### Path Half Segments
The `pathHalfSegments` list contains 3D points that define the train's path with sub-tile precision. Each tile contributes **two half segments** to the path:
1. A point at the **center of the tile** (`new Vector3(tile.x, 0, tile.y)`)
2. A point at the **exit edge** of the tile (`tile + halfOrientation`)

**Why half segments exist**: Half segments enable turns. When a train enters a curved rail, the path goes from the entry edge → tile center → exit edge. The `headInterpolation` value tracks the train's position along these segments, where each half segment represents 1 unit of interpolation distance.

##### Entering a Tile
When the train's `headInterpolation` exceeds the current path length, the `AdvancePath` method is called:
1. **Check compatibility**: The game queries `GetTrainOrientations(newTile)` to get valid entry directions for the new tile
2. **Validate entry**: If the train's current `headOrientation` is in the valid orientations list, entry is allowed
3. **Determine exit direction**: `GetNextTrainOrientation(newTile, headOrientation)` returns the direction the train will exit the tile. For straight rails, this is the opposite of entry. For curved rails or intersections, it may differ
4. **Update path**: Two new half segments are added (tile center and exit point), and `GameManager.TrainEnterTile` notifies the rail structure

##### Exiting a Tile
When a train fully leaves a tile (the tail passes the exit), `StepOutOfTile` is called:
1. The tile is removed from `tilesPath`
2. The corresponding two half segments are removed from `pathHalfSegments`
3. `headInterpolation` is decremented by 2 (since two half segments were removed)
4. `GameManager.TrainExitTile` notifies the rail structure

##### Cart Positioning
Each cart's position is calculated by interpolating along the path half segments. The `InterpolateCart` method finds points 0.5 units ahead and behind the cart's center interpolation value, then positions the cart between them with appropriate rotation. This ensures carts smoothly follow curves.

#### Key Classes
- **Train**:
  - Manages train movement, speed, and pathfinding.
  - Handles cart additions and removals.
  - Key Methods:
    - `Update`: Updates the train's position and speed.
    - `AddCart`: Adds a new cart to the train.
    - `Crash`: Handles train collisions.

- **Cart**:
  - Represents individual train components.
  - Handles collision detection and triggers train crashes.

- **DynamicRail**:
  - Handles train entry/exit and manages orientations.
  - Key Methods:
    - `TrainEnter`: Adds a train to the rail.
    - `TrainExit`: Removes a train from the rail.

#### Train Movement
- Trains move along a path defined by rails, managed by the `DynamicRail` class.
- Speed and acceleration are updated each frame using the `Train` class.
- Collisions are detected and handled, with the `Crash` method ensuring proper state updates.

#### Code Walkthroughs
##### Moving a Train
```csharp
train.Update();
```
- Updates the train's position and speed.
- Handles pathfinding and collisions.

##### Adding a Cart
```csharp
train.AddCart(cartPrefab);
```
- Adds a new cart to the train.
- Updates the train's path and state.

##### Handling Collisions
```csharp
train.Crash();
```
- Stops the train and updates its state.

---

### Signals System Guide

#### Overview
The signals system manages signal propagation and network management. It ensures that signals are transmitted between connected ports.

#### Signal Network Graph

##### What It Is
The `PortNetworkGraph` is an undirected graph data structure that tracks all signal connections in the game. Ports are vertices, and wires connecting them are edges. This graph is the backbone of the signal system, determining which ports share signal values.

##### Why It Exists
Without a network graph, determining signal connectivity would require traversing wires every time a signal is read or written—an O(n) operation per access. The graph enables **channel-based signal sharing**: all ports in a connected subgraph share a single `Channel` object. Writing to any port in the network updates the shared channel, and reading from any port returns the same value. This reduces signal operations to O(1).

##### How It Works

**Channel Assignment with BFS**: When wires are connected or disconnected, the graph uses Breadth-First Search (`AssignSignalChannelBFS`) to propagate channel assignments:
- **Connecting two unconnected ports**: A new `Channel` is created and assigned to both ports via BFS
- **Connecting to an existing network**: The new port inherits the existing network's channel
- **Disconnecting a wire**: If the network splits into two separate subgraphs, one subgraph gets a new channel while the other keeps the original

**Signal Flow Per Tick**:
1. `ResetSignalChannels()`: All channels are reset to 0 at the start of each tick
2. Sensors write values to their output ports (which write to their shared channels)
3. Processors read from input ports, compute results, and write to output ports
4. Actuators read from input ports to determine their behavior

**Channel Semantics**: When multiple sources write to the same channel in a single tick, the channel takes the **maximum** value (`Mathf.Max`). This allows multiple sensors or processors to contribute to a shared signal line, with the strongest signal winning.

#### Key Classes
- **Port**:
  - Represents a connection point for signals.
  - Supports reading and writing signal values.
  - Key Methods:
    - `Write`: Writes a signal value to the port.
    - `Read`: Reads the signal value from the port.

- **PortNetworkGraph**:
  - Manages the graph of connected ports.
  - Propagates signals using BFS.
  - Key Methods:
    - `AssignSignalChannelBFS`: Propagates signals through the network.

- **Channel**:
  - Represents a signal channel.
  - Stores and propagates signal values.
  - Key Methods:
    - `Write`: Updates the signal value.
    - `Reset`: Clears the signal value.

#### Signal Propagation
- Signals are written to ports and propagated through the network graph, managed by the `PortNetworkGraph` class.
- Channels, represented by the `Channel` class, store signal values and reset each frame to ensure accurate data flow.
- The `Port` class handles individual signal connections, enabling reading and writing of signal values.

#### Code Walkthroughs
##### Writing a Signal
```csharp
port.Write(signalValue);
```
- Updates the signal channel with the new value.

##### Propagating Signals
```csharp
networkGraph.AssignSignalChannelBFS(startPort, signalChannel);
```
- Propagates the signal using BFS.
- Updates connected ports and channels.

##### Managing Signal Networks
```csharp
networkGraph.ConnectWire(wire, port1, port2);
```
- Connects two ports with a wire.
- Updates the network graph.

- **Purpose**: Manage signal connections and propagation.
- **Key Classes**:
  - `Port`: Represents a connection point for signals.
  - `PortNetworkGraph`: Manages the graph of connected ports.
  - `Channel`: Represents a signal channel.
- **Responsibilities**:
  - Signal reading/writing and network management.

---

## Code Flow Examples

#### Adding a Structure
```csharp
GameManager.Instance.AddStructure(tile, orientation, structurePrefab);
```
- Checks if the tile is available.
- Instantiates the structure and updates the game state.

#### Processing a Recipe
```csharp
recipeMachine.ProcessMachine();
```
- Consumes ingredients from input resources.
- Produces outputs and moves them to the output conveyor.

#### Moving a Train
```csharp
train.Update();
```
- Updates train speed and position.
- Handles train entry/exit on rails.

#### Signal Propagation
```csharp
port.Write(signalValue);
```
- Updates the signal channel and propagates the value to connected ports.

---

## Save/Load System Guide

### Overview
The save/load system enables game persistence by serializing all game objects and their states to JSON. It uses a phased approach to handle complex object relationships and dependencies.

### Key Components

#### ISavable Interface
All persistent objects implement the `ISavable` interface:
```csharp
public interface ISavable
{
    int ID { get; set; }                    // Unique identifier for cross-references
    string TypeName { get; }                 // Type name for prefab lookup during load
    bool ShouldInstantiateOnLoad();          // Whether to create a new GameObject on load
    string GetStateJson();                   // Serialize object state to JSON
    void RestoreStateJson(string stateJson, Dictionary<int, ISavable> idLookup);  // Restore state from JSON
}
```

**Instantiable vs Non-Instantiable Objects**:
- **Instantiable** (`ShouldInstantiateOnLoad() = true`): Objects like structures, trains, and resources that need a new GameObject created on load
- **Non-Instantiable** (`ShouldInstantiateOnLoad() = false`): Objects like `Port` (found as children of structures) and `Channel` (created programmatically) that are located or created through custom logic

#### CombinedState Pattern
For inheritance hierarchies, derived classes use `CombinedState` to serialize both base and derived state:
```csharp
CombinedState combinedState = new() {
    baseState = base.GetStateJson(),
    inheritedState = JsonConvert.SerializeObject(derivedFields)
};
```

### Save Process

1. **Collect ISavables**: Find all `MonoBehaviour` objects implementing `ISavable`, plus non-MonoBehaviour savables like `Channel`
2. **Save GameManager State**: Serialize game-wide data (tick count, materials, tile mappings, port connections)
3. **Serialize Each Object**: For each ISavable, create a `SavebleEntry` containing:
   - Unique ID, given by the SaveManager
   - Transform data (position, rotation, scale) for instantiable objects
   - Type name for prefab lookup
   - Custom state JSON from `GetStateJson()`
4. **Write to File**: Save as formatted JSON to `Application.persistentDataPath`

### Load Process (Four Phases)

**Phase 1 - Instantiate Objects**:
```csharp
foreach (SavebleEntry entry in saveData.savables) {
    if (!entry.shouldInstantiateOnLoad) continue;
    GameObject obj = Instantiate(PrefabRegistries.Instance.savables[entry.type], ...);
    idLookup[entry.id] = obj.GetComponent<ISavable>();
}
```
Creates GameObjects for all instantiable savables using registered prefabs.

**Phase 2 - Resolve Non-Instantiables**:
- **Ports**: Located by name within their parent structure's hierarchy
- **Channels**: Created as new objects (they have no GameObject)

**Phase 3 - Restore GameManager State**:
Rebuilds the game's tile mappings, signal network connections, and train registrations using the `idLookup` dictionary.

**Phase 4 - Restore Object States**:
```csharp
foreach (SavebleEntry entry in saveData.savables) 
    idLookup[entry.id].RestoreStateJson(entry.stateJson, idLookup);
```
Each object deserializes its state, using `idLookup` to resolve cross-references to other objects.

### Cross-Reference Resolution
Objects reference each other by ID rather than direct pointers. During save, references are converted to IDs:
```csharp
// In Train.GetStateJson()
carts.ConvertAll(cart => cart.ID)
```
During load, IDs are resolved back to object references:
```csharp
// In Train.RestoreStateJson()
carts = cartIds.ConvertAll(cartId => (Cart)idLookup[cartId]);
```

### Unique ID Generation
`SaveManager.GenerateUniqueId()` provides monotonically increasing IDs. After loading, the ID generator is reset to one past the highest loaded ID to prevent collisions with new objects.

---

## Feature Roadmap

1. Structures placement and hotbar UI - Done!
2. Circuit components and wiring - Done!
3. Trains and rails network - Done!
4. Serialization/deserialization - Done!
5. Items and Conveyor belts - Done!
6. Machines and funnels - Done!
7. Code documentation
8. Cargo carts
9. Pickup/Drop items interactively

## Bugs/Improvements

- internal clocks for funnels instead of relying on global ticks

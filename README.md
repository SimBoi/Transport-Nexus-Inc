# Transport Nexus Inc

Transport Nexus Inc is a simple tech game focusing on the transportation aspect and designed for ease of playing on both pc and mobile platforms.

## Features

- Structures placement and hotbar UI
- Circuit components and wiring
- Trains and rails network
- Serialization/deserialization
- Items and Conveyor belts
- Machines and funnels
- Cargo carts
- Camera Navigation
- Storage Containers & Storage Monitor
- Terrain Generation + building limits
- Selection tools

## WIP

- Migrate to new input system & use Canvas for building UI

## Roadmap

- Menus
- Sounds
- Models & Animations
- Game loop planning
- Code documentation - I think its too late for that now. Am I cooked chat?

## Bugs/Improvements

- Remove funnel enable/disable
- Select tool for bulk operations
- Train IDs + Pressure Plates
- internal clocks for funnels instead of relying on global ticks
- Fluids & water bodies
- PCBs (create parts that contain complex signal logic)
- Train not removing itself from all rail tiles properly
- Attempting to place a train on an isolated station, it gets added to its list of trains but the train is not spawned
- drag to extend structures instead of repeated clicks
- dont drop items from structures when rotating them
- Optimize tile rendering in chunks
- improve building structures with orientation
- render focus structure in a separate camera on top of everything


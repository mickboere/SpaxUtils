StateMachine implementation rules:

- Abstract "base" classes may never contain input-connections, these are determined by their implementation.
- StateNodes can only connect to nodes having an input-connection of type Connections.StateComponent.
- TransitionNodes must always connect to a StateNode, either directly or indirectly.
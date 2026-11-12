// The preview of a factory while its machines are chosen in 3D (part 2b(2)), apart from three.js.
import { ConnectionSummary } from '../../api/models/connection-summary';
import { MapConveyor } from '../../api/models/map-conveyor';
import { PositionDocument } from '../../api/models/position-document';

/**
 * Where a line crosses into or out of the factory, to mark it: on a belt or pipe across, its end at the factory's side;
 * on one of its own, its end at the far side; otherwise where the building inside stands.
 */
export function handoverPoint(
  connection: ConnectionSummary,
  direction: 'output' | 'input',
  conveyorOf: (id: string) => MapConveyor | undefined,
  positionOf: (id: string) => PositionDocument | undefined,
): PositionDocument | null {
  const first = (conveyor: MapConveyor) => conveyor.curve[0]?.location;
  const last = (conveyor: MapConveyor) => conveyor.curve[conveyor.curve.length - 1]?.location;
  const across = connection.outside ? conveyorOf(connection.outside.id) : undefined;
  if (across) return (direction === 'output' ? first(across) : last(across)) ?? null;
  const own = conveyorOf(connection.inside.id);
  if (own) return (direction === 'output' ? last(own) : first(own)) ?? null;
  return positionOf(connection.inside.id) ?? (connection.outside ? (positionOf(connection.outside.id) ?? null) : null);
}

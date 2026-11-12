import { ConnectionSummary } from '../../api/models/connection-summary';
import { MapConveyor } from '../../api/models/map-conveyor';
import { PositionDocument } from '../../api/models/position-document';
import { handoverPoint } from './factory-preview';

describe('factory preview', () => {
  describe('where a line crosses', () => {
    const at = (x: number): PositionDocument => ({ x, y: 0, z: 0 });
    const belt = (id: string, from: number, to: number): MapConveyor => ({
      id,
      className: 'Build_ConveyorBeltMk1_C',
      kind: 'belt',
      contents: null,
      curve: [from, to].map((x) => ({ location: at(x), arriveTangent: at(0), leaveTangent: at(0) })),
    });
    const conveyors = new Map([['out', belt('out', 10, 90)], ['in', belt('in', 100, 190)]]);
    const positions = new Map([['smelter', at(0)], ['box', at(300)]]);
    const point = (connection: ConnectionSummary, direction: 'output' | 'input') =>
      handoverPoint(connection, direction, (id) => conveyors.get(id), (id) => positions.get(id));
    const ref = (id: string) => ({ id, className: 'x' });

    it('an output from a machine onto a belt crosses where the belt starts', () => {
      expect(point({ inside: ref('smelter'), outside: ref('out'), items: [] }, 'output')).toEqual(at(10));
    });

    it('an input on a belt into a machine crosses where the belt ends', () => {
      expect(point({ inside: ref('smelter'), outside: ref('in'), items: [] }, 'input')).toEqual(at(190));
    });

    it('an output into a container of its own is at the container', () => {
      expect(point({ inside: ref('box'), outside: null, items: [] }, 'output')).toEqual(at(300));
    });
  });
});

import * as THREE from 'three';

import { Position } from '../../shared/display';
import { boxGeometry } from './box-geometry';

describe('a box in 3D', () => {
  // A box of 2 by 2 by 2 around (0, 0, 0), corners in the order a footprint gives them: x, then y, then z.
  const box: Position[] = [];
  for (const x of [-1, 1]) for (const y of [-1, 1]) for (const z of [-1, 1]) box.push({ x, y, z });

  /** Each triangle's normal (by its winding) and its middle. */
  function triangles(
    geometry: THREE.BufferGeometry,
  ): { normal: THREE.Vector3; middle: THREE.Vector3 }[] {
    const position = geometry.getAttribute('position');
    const result = [];
    for (let i = 0; i < position.count; i += 3) {
      const [a, b, c] = [i, i + 1, i + 2].map((k) =>
        new THREE.Vector3().fromBufferAttribute(position, k),
      );
      result.push({
        normal: new THREE.Triangle(a, b, c).getNormal(new THREE.Vector3()),
        middle: a.clone().add(b).add(c).divideScalar(3),
      });
    }
    return result;
  }

  it('faces every side outwards', () => {
    const geometry = boxGeometry(box, (p) => new THREE.Vector3(p.x, p.y, p.z));

    expect(triangles(geometry).every(({ normal, middle }) => normal.dot(middle) > 0)).toBe(true);
  });

  // The scene's y is the game's z: swapping two axes mirrors the box, which turns its sides inside out.
  it('faces every side outwards when placed with the game height as the scene y', () => {
    const geometry = boxGeometry(box, (p) => new THREE.Vector3(p.x, p.z, p.y));

    expect(triangles(geometry).every(({ normal, middle }) => normal.dot(middle) > 0)).toBe(true);
  });
});

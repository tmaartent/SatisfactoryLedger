import * as THREE from 'three';

import { Position } from '../../shared/display';
import { Box } from './area-model';

/** The six sides of a box, as corner numbers (see Box): drawn as two triangles each. */
const SIDES = [[0, 1, 3, 2], [4, 6, 7, 5], [0, 4, 5, 1], [2, 3, 7, 6], [0, 2, 6, 4], [1, 5, 7, 3]];

/**
 * A box as a three.js geometry, each corner placed in the scene by `at`. Each side faces away from the box's middle,
 * whatever `at` does: placing the game's height as the scene's y mirrors the box, which would turn its sides inside out,
 * and a material drawing one side only would leave out the walls in front.
 */
export function boxGeometry(box: Box, at: (p: Position) => THREE.Vector3): THREE.BufferGeometry {
  const corners = box.map(at);
  const middle = corners.reduce((sum, corner) => sum.add(corner), new THREE.Vector3()).divideScalar(corners.length);
  const positions: number[] = [];
  for (const side of SIDES) {
    const [a, b, , d] = side.map((k) => corners[k]);
    const facing = new THREE.Vector3().subVectors(b, a).cross(new THREE.Vector3().subVectors(d, a));
    const outwards = facing.dot(new THREE.Vector3().subVectors(a, middle)) >= 0;
    const [p, q, r, s] = outwards ? side : [...side].reverse();
    for (const k of [p, q, r, p, r, s]) positions.push(corners[k].x, corners[k].y, corners[k].z);
  }
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  geometry.computeVertexNormals();
  return geometry;
}

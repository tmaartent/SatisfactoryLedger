import * as THREE from 'three';

/** A way in cyan, a way out amber: told apart at a glance, as well as by where they point. */
const colours = { in: '#4dd0e1', out: '#ffb300' } as const;

// Shared by every arrow: made once, never disposed.
const shaft = new THREE.CylinderGeometry(0.22, 0.22, 1.8, 8).translate(0, -0.6, 0);
const head = new THREE.ConeGeometry(0.75, 1.2, 12).translate(0, 0.9, 0);
const materials = {
  in: new THREE.MeshBasicMaterial({ color: colours.in, depthTest: false }),
  out: new THREE.MeshBasicMaterial({ color: colours.out, depthTest: false }),
};
const up = new THREE.Vector3(0, 1, 0);

/**
 * An arrow on a splitter's or merger's way in or out (asked 2026-11-26), in a 3D view: 3 metres long, lying flat, its
 * middle at `at`, pointing the way the items go, `along` the game's x and y (the scene's x and z). Drawn over
 * everything, so a belt in front doesn't hide it.
 */
export function flowArrow(
  at: THREE.Vector3,
  along: { x: number; y: number },
  way: 'in' | 'out',
): THREE.Group {
  const arrow = new THREE.Group();
  for (const geometry of [shaft, head]) {
    const piece = new THREE.Mesh(geometry, materials[way]);
    piece.renderOrder = 10;
    arrow.add(piece);
  }
  arrow.quaternion.setFromUnitVectors(up, new THREE.Vector3(along.x, 0, along.y).normalize());
  arrow.position.copy(at);
  return arrow;
}

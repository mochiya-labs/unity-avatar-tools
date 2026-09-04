# Loading in Three.js

Register both the VRM plugin and the Mochiya lilToon plugin on the same `GLTFLoader`:

```ts
import { GLTFLoader } from "three/examples/jsm/loaders/GLTFLoader.js";
import { VRMLoaderPlugin } from "@pixiv/three-vrm";
import {
  GLTFLilToonExtension,
  LilToonRendererAdapter,
} from "three-liltoon";

const rendererAdapter = new LilToonRendererAdapter();
const loader = new GLTFLoader();

loader.register((parser) => new VRMLoaderPlugin(parser));
loader.register(
  (parser) => new GLTFLilToonExtension(parser, { rendererAdapter }),
);

const gltf = await loader.loadAsync(url);
const vrm = gltf.userData.vrm;
scene.add(vrm?.scene ?? gltf.scene);
```

The lilToon plugin replaces only materials carrying `MOCHIYA_materials_liltoon`. Other materials continue through the normal Three.js/VRM material path.

For `.vrm` files, animation remains the responsibility of the consuming application's `three-vrm` integration. The exporter keeps the standard VRM humanoid, expressions, constraints, and spring-bone data in the file; the Mochiya extension only changes how marked materials are rendered.

The current extension loader reads `texCoord: 0`. Exported materials therefore use UV0. A future extension revision is required before nonzero material UV channels can be relied on.

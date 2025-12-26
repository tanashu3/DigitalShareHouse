This Asset has some custom Shaders. Next, I will briefly explain how they work:

StandardDouble: This is a simple modification to the Standard Shader that Unity has by default, with the difference that it draws both sides of the model's faces.

EffectDistorcion: Generates a "distortion" effect, with which effects such as stale wind, sudden movements or sudden expansion can be achieved. It is used by the "Scream" prefab, to generate a small distortion.

PriorityParticleAlpha: Similar to ParticleAlphaSmoth, but with a high draw priority.
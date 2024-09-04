# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [1.2.1] - 2024-05-11
- Add FRP-Pipeline to replace URP
- Remove FSR3
- Remove All PostProcessing

## [1.2.1] - 2024-04-22 
**This update is very important**
- m_ActiveRenderPassQueue move to FRPCameraData, because different Cameras have different Renderer
- If there is a Pass after post-processing, FinalPass should be executed.
- If m_cameraTargetResolved is present, it should not be executed ConfigureCameraTarget After Post-Processing
- disable frp when camera is overlay
- Increase the range of taa blendfator to 1
- Fixed: camera with stack and finalpost

## [1.2.0-pre.2] - 2024-04-09
- Fixed SSGI Wrong if depthScale < 1
- If there are not multiple passes that require normals, the pass that reconstructs the normal is not executed
- HBAO Optimisation: hbao's After Opaque mode Mixes directly to Opaque
- Add ResetMatrix Option After TAA

## [1.2.0-pre.1] - 2024-04-08
- Refactored post-processing code to fix no depth/stencil buffer error after FinalBlit
- Update To Unity 2022.3.17, URP 14.0.9
- Enable SSGI in SceneView
- All Volume Check in Feature Class(No Finish)
- Remove hbao DOF Check
- Add option to Override URP Post-Processing or not
- Fixed normal reconstruct rt format support for mobile

## [1.1.0] - 2024-03-19

- Planar Reflection Add Depth Aniso
- Add FSR3.0
- RTHandle Move To FRP FRPCameraData
- improve Post-Processing
- Fix some Bug

## [1.0.0] - 2023-12-18

- Add Planar Reflection
- Add HBAO Volume
- Add AreaLight
- Add SSGI



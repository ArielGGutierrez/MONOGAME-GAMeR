# MONOGAME GAMeR
## Description
An alternative to using high-poly models would be to have a GPU calculate more detail out of low poly models.  Generic Adaptive Mesh Refinement (GAMeR) uses the CPU to create a list of Adaptive Refinement Patterns (ARPs). These ARPs are triangles encoded in Barycentric coordinates which are subdivided to a given level of detail. The list of adaptive refinement patterns is passed on to the GPU along with a triangle from the mesh and a special tag that dictates the amount of detail the triangle should have. The vertex shader then uses the triangle from the mesh to interpolate the positions of the vertices from the ARP list. In addition, a displacement function can be used to displace these vertices further along their normal to simulate more detail. This frees up the Pixel shader to be used for other purposes.

## Screenshots
![image](https://github.com/ArielGGutierrez/MONOGAME-GAMeR/assets/78765691/c9c21cce-a322-489c-8dc0-bb91db0c8e6d)
![image](https://github.com/ArielGGutierrez/MONOGAME-GAMeR/assets/78765691/de2a901b-126b-4d9a-8055-06bf601be0b9)
![image](https://github.com/ArielGGutierrez/MONOGAME-GAMeR/assets/78765691/c3199825-88b2-45d1-bd1e-ceba97e8d499)

![image](https://github.com/ArielGGutierrez/MONOGAME-GAMeR/assets/78765691/5edbc2e1-a06a-4563-a1d9-c6e3b4973adb)
![image](https://github.com/ArielGGutierrez/MONOGAME-GAMeR/assets/78765691/d57070e3-daed-40ff-b416-668a9cf3a8e1)

![image](https://github.com/ArielGGutierrez/MONOGAME-GAMeR/assets/78765691/9cf18841-6fd5-45e8-9f8a-804dc3e8b79d)

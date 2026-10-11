# Marca monocromática de Cogana Admin

Propuesta aprobada por el usuario el 11/10/2026. Assets preparados con la herramienta integrada image_gen a partir de la propuesta aprobada, conservando hojas, cuenco, granos y cuchara; el logo completo conserva el nombre Cogana y el doble círculo. No usar el logo completo como ícono pequeño: el símbolo compacto mantiene la lectura.

- CoganaLogo.png: emblema negro con fondo transparente para cabeceras blancas.
- CoganaIcono.png: símbolo blanco en una baldosa negra con esquinas transparentes, para la barra lateral, estados del acceso e ícono de Windows.
- Cogana.ico: conversión de CoganaIcono.png a contenedor Windows de 16, 20, 24, 32, 40, 48, 64, 128 y 256 píxeles. Conserva la transparencia. No cambia el diseño.

App.xaml centraliza ambas imágenes y el ícono de todas las ventanas. El proyecto establece ApplicationIcon para el ejecutable y Publicar-Instalador.ps1 transmite el mismo ICO a Velopack. Las imágenes PNG/ICO están embebidas; la aplicación no depende de archivos temporales ni del directorio de trabajo.

## Prompts de los assets finales

Ícono (image_gen, fondo transparente):

Edit target: the approved Cogana monochrome presentation board. Extract ONLY the large app icon in the RIGHT column exactly as approved: black rounded square with three white leaves with black veins, white grain bowl with black grain details, and white spoon below. Preserve its design, proportions and orientation exactly, do not redesign or add elements. Produce one standalone square application icon asset with the rounded black tile nearly filling the canvas, equal small transparent margin around it, genuinely transparent alpha outside the rounded corners. Crisp high resolution flat black/white, clean edges, no shadow, no texture, no labels, no letters, no presentation board, no other icons or logos. Reduce only the finest grain detail as needed for small-size clarity, retain the spoon and three leaves. The entire output is this single approved icon.

Logo (image_gen, fondo transparente):

Edit target: approved Cogana monochrome presentation board. Extract ONLY the full black logo from LEFT column, exactly as approved, standalone square logo asset. Preserve exact brand shapes and typography: three leaves above the bowl of grains, spoon, italic cursive 'Cogana' (C-o-g-a-n-a) across center, double open circular outline, grains along bottom. All ink solid near-black #151515 on genuinely transparent alpha background, including transparent spaces inside the circle, leaves and bowl. No white background rectangle. No labels, no other icon, no columns or presentation board. Preserve elegant approved design faithfully, do not redesign. Center the complete emblem nearly filling a square canvas with small equal transparent padding, sharp high resolution edges, flat artwork, no bevels, shadows or textures. Text exactly 'Cogana'. This is the final production asset for a white WPF login card.

# eng/

Scripts y herramientas de build para el repo. Nada aqui se shippea a usuarios finales.

## Scripts

| Script | Descripcion |
|---|---|
| `pack-packages.ps1` | Empaqueta `Dorn.Messaging.Contracts`, `Dorn.Messaging` y `Dorn.SharedKernel` hacia `./artifacts` |
| `pack-templates.ps1` | Empaqueta `templates/webapi` como paquete NuGet template hacia `./artifacts` |

```bash
pwsh eng/scripts/pack-packages.ps1
pwsh eng/scripts/pack-templates.ps1
```

## Packaging

`eng/packaging/Dorn.Templates.WebApi/` es un proyecto de MSBuild que solo sirve para construir el `.nupkg` del template. Esta fuera de `templates/webapi/` para que nunca se instancie en un proyecto generado.

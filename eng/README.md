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

## Release y publicacion (NuGet.org)

| Aspecto | Detalle |
|---|---|
| Trigger | `git tag vX.Y.Z && git push --tags` dispara `.github/workflows/publish.yml` |
| Version | Manual, derivada del tag: `VERSION=${GITHUB_REF_NAME#v}`. No hay tooling de bump automatico |
| Gate | El job `publish` declara `needs: test`, y `test` llama a la misma matrix reusable de 2 OS (`.github/workflows/build-test.yml`) que usa `ci.yml`. Si cualquier celda falla, `publish` no corre |
| Auth | Trusted Publishing (OIDC) via `NuGet/login@v1` + `permissions: id-token: write`. No hay `NUGET_API_KEY` guardado como secret |
| Paquetes publicados | `Dorn.Messaging.Contracts`, `Dorn.Messaging`, `Dorn.SharedKernel`, `Dorn.Cli`, `Dorn.Templates.WebApi` |
| Relacion con `ci.yml` | Mismo workflow reusable; `ci.yml` excluye tags `v*` (`tags-ignore`) para que un push de tag no dispare una segunda corrida independiente de la matrix |
| Restriccion de archivo | `publish.yml` **no debe renombrarse ni moverse** — la politica de Trusted Publishing en NuGet.org esta atada al nombre del archivo del workflow |

Ver `docs/adr/0020-nuget-trusted-publishing-and-test-gated-releases.md` para el razonamiento completo detras de estas decisiones.

### Validacion de la politica de Trusted Publishing (runbook manual)

Este paso es operacional, no un task de codigo, y requiere acceso de owner del repo:

1. Despues de mergear este cambio, pushear un tag prerelease, ej. `v1.0.2-rc.1` (mismo
   precedente que `v0.1.0-test`).
2. Confirmar que la matrix corre exactamente una vez, disparada por `publish.yml` (`ci.yml`
   no deberia re-disparar una corrida independiente).
3. Si la politica de Trusted Publishing no coincide, `NuGet/login@v1` debe fallar **antes**
   de `dotnet nuget push` — sin publicar nada.
4. Si el login tiene exito, verificar que el paquete prerelease queda oculto de la busqueda
   por default y de `dotnet tool install` sin `--prerelease`.
5. Registrar el resultado (fecha, tag usado, resultado) como referencia para futuras releases.

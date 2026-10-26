# Conectar o CRITICAL! ao Codex

Preparação em 26/10/2026. Editor encontrado: **Unity 2022.3.62f3**, projeto `critical/` neste repositório. Branch de trabalho: **demo**.

## Estado desta preparação

- Branch `demo` criada e publicada em `origin`.
- MCP `unityMCP` cadastrado no Codex com URL `http://localhost:8080/mcp`.
- Pré-requisitos confirmados neste computador: `uv 0.13.5` e Python `3.12.15`. A descoberta e a execução do Python pelo uv foram testadas. **Não é necessário repetir a instalação; continue no passo 3.**
- Pacote MCP ainda não instalado no Unity nesta etapa. Servidor e conexão com o Editor ainda não verificados.
- Scripts, cenas e assets de gameplay não foram alterados nesta preparação.
- Alterações locais anteriores em `README.md` e `.obsidian/workspace.json` foram preservadas e não fazem parte do commit de preparação.

O `com.unity.pipeline` usado pela skill Unity Pipeline requer Unity 6; por isso esta conexão usa **MCP for Unity**, que declara suporte a Unity 2021.3 ou mais recente. Não é necessário migrar o projeto para Unity 6.

## Passos no computador

1. Salve seu trabalho no Unity. Confirme que o Editor está aberto no projeto `critical/` deste repositório.
2. Instale o `uv` no PowerShell:

   ```powershell
   winget install --id astral-sh.uv -e
   ```

   Abra um PowerShell novo e confirme `uv --version`. Para preparar o Python 3.12 também, execute:

   ```powershell
   uv python install 3.12
   ```

   O assistente do MCP orienta as dependências que ainda faltarem. Se o Unity já estava aberto antes da instalação e não detectar o `uv`, use o seletor de caminho do assistente ou reabra o Unity após salvar.

3. No Unity, abra **Window > Package Manager**. Clique **+ > Add package from git URL...** e cole:

   ```text
   https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v10.3.0
   ```

   A versão está fixada para manter a instalação reproduzível. A tag foi conferida no repositório oficial. Aguarde a importação e a compilação.

4. Abra **Window > MCP for Unity**. Conclua o assistente de dependências. Selecione transporte **HTTP**, endereço local `http://localhost:8080`, e inicie o servidor local e a sessão/ponte do Unity. O endpoint que o Codex usa termina em `/mcp`; o campo base do servidor pode mostrar apenas o endereço sem esse sufixo.

   Os nomes dos botões podem variar entre o assistente e o painel principal: procure **Start Local HTTP Server**, **Start Session** ou **Start Bridge**, conforme o painel apresentado. Mantenha o processo do servidor aberto enquanto estiver trabalhando.

5. Volte a esta conversa e avise que iniciou o servidor. Se `unityMCP` ainda não aparecer entre as integrações do Codex, salve o trabalho e reabra o aplicativo para carregar a configuração. Não é necessário rodar novamente o cadastro abaixo: ele já foi executado.

   ```powershell
   codex mcp add unityMCP --url http://localhost:8080/mcp
   ```

## Como vamos confirmar a conexão

O cadastro da URL não significa que a conexão está funcionando. A verificação será ler as instâncias do Unity, selecionar este projeto, obter a hierarquia/cena ativa e ler o Console pelo MCP. Só depois serão feitas alterações de cena/prefab pelo Editor.

O primeiro teste de gameplay será menu > sala > movimento > ataque > dano > morte/reinício, usando o personagem, o rato e o mapa existentes. Entrar no Play Mode sozinho não prova que o ciclo funciona.

## Se houver problema

- **Git não encontrado ao adicionar o pacote:** reinicie o Unity depois de confirmar que `git --version` funciona em um terminal novo.
- **Dependência Python/uv não encontrada:** confira o assistente e `uv --version`; se o PATH mudou, reabra o Editor após salvar.
- **PowerShell não reconhece `uv` logo após instalar:** abra uma janela nova para carregar o PATH atualizado. A URL Git do pacote é para o campo do Package Manager do Unity, não para o PowerShell.
- **Erro de atalho de versão do Python no Windows:** nesta preparação o Python foi instalado, mas o uv deixou uma junction de versão que não resolvia. Foi removida apenas essa junction, preservando o interpretador. `uv python find 3.12` e execução via `uv run --no-project --no-config --no-python-downloads --python 3.12 python --version` passaram. [Relato no projeto uv](https://github.com/astral-sh/uv/issues/19622).
- **Erros vermelhos de compilação:** envie o texto do primeiro erro do Console. A conexão depende de o pacote conseguir compilar.
- **Servidor em outra porta:** use a mesma porta no painel do Unity e na configuração `unityMCP` do Codex. A preparação atual usa 8080.
- **URL cadastrada, mas sem Editor listado:** mantenha o Unity e o servidor abertos; confira se a sessão/ponte foi iniciada no painel MCP.

## Fontes

- [MCP for Unity: instalação oficial](https://github.com/CoplayDev/unity-mcp/blob/main/website/docs/getting-started/install.md)
- [MCP for Unity: manifesto e versão mínima](https://github.com/CoplayDev/unity-mcp/blob/main/MCPForUnity/package.json)
- [uv: instalação no Windows](https://docs.astral.sh/uv/getting-started/installation/)
- [uv: instalação do Python](https://docs.astral.sh/uv/guides/install-python/)
- [Documentação oficial OpenAI: MCP no Codex](https://developers.openai.com/codex/mcp)

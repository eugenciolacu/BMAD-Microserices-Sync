https://docs.bmad-method.org/
https://github.com/bmad-code-org/BMAD-METHOD

install BMAD:
npx bmad-method install
npx bmad-method@6.0.3 install

command format for BMAD 6:
/bmad-help
/bmad-help [your question]

brainstorming:
/bmad-brainstorming

research:
/bmad-bmm-research

reate-product-brief:
/bmad-bmm-create-product-brief

Load the PM agent (in a new chat):
/bmad-agent-bmm-pm

Run the prd workflow:
/bmad-bmm-create-prd

Validate PRD:
/bmad-bmm-validate-prd

Edit PRD, Improve and enhance an existing PRD:
/bmad-bmm-edit-prd

Create Epics and Stories:
/bmad-bmm-create-epics-and-stories

Load the Architect agent (in a new chat):
/bmad-agent-bmm-architect

Run create-architecture:
/bmad-bmm-create-architecture

Validate a documen:
/bmad-bmm-validate-document [documant name]

Generate project-context.md (recomended after generation of architecture, optionally, my thoughts, can be re-generated at any step)
/bmad-bmm-generate-project-context

Create epics and stories (preload PM agent /bmad-agent-bmm-pm in a new chat):
/bmad-bmm-create-epics-and-stories

Check implimentation readiness (preload architecture agent /bmad-agent-bmm-architect in a new chat):
/bmad-bmm-check-implementation-readiness

Sprint planning (preload scrum master agent /bmad-agent-bmm-sm in new chat) This creates sprint-status.yaml to track all epics and stories.
/bmad-bmm-sprint-planning

Create story (with preloaded scrum master agent):
/bmad-bmm-create-story

Implement the story (use dev agent /bmad-agent-bmm-dev):
/bmad-bmm-dev-story

Quality validation, code review
/bmad-bmm-code-review

Epic retrospective, after completeing stories in a epic (preload Scrum Master agent /bmad-agent-bmm-sm):
/bmad-bmm-retrospective

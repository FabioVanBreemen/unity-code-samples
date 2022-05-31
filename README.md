# Unity C# toelichting
In deze repository staan enkele classes van mijn game project.

Kleine uitleg;
- PlayerLogic voert een interactie uit met een item (zaklamp, sleutel, deur, etc.) via de Interface van InteractableLogic (de superclass van alle item interacties).
- ItemLogic is een subclass van InteractableLogic en bevat logica die alleen van toepassing is voor items die opgepakt kunnen worden in de hotbar, zoals een zaklamp. Denk hierbij aan het maken van een geluid en het instellen van een 'sound trigger' zodra dit item in aanraking komt met iets anders.
- Sound triggers, zoals ik ze noem, zijn SphereColliders die gebruikt worden om ervoor te zorgen dat de vijanden gehoor hebben. Stel dat bijvoorbeeld een deur geopent wordt door de speler, dan wordt de sound trigger van de deur ingesteld op de vooraf bepaalde waarde die in de SoundTriggerScriptableObject staat. Als een vijand in aanraking komt met die sound trigger dan reageert hij daar op en rent hij richting de geluidsbron.

Een korte demo zien en meer weten over mij en mijn project? Kijk de korte video (nog geen 4 min); https://youtu.be/LqI5bxuMgOc

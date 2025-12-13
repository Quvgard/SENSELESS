using UnityEngine;

/// <summary>
/// Простой пример IInteractable.
/// Для теста повесь этот скрипт на InteractCube (и любой объект с коллайдером).
/// При взаимодействии показывает диалоговое окно с забавными фразами.
/// </summary>
public class SimpleInteractable : MonoBehaviour, IInteractable
{
    [Header("Описание для подсказок")]
    [SerializeField] private string description = "Нажмите E, чтобы взаимодействовать";

    [Header("Диалоговые реплики (заглушки)")]
    [TextArea]
    [SerializeField]
    private string[] dialogueLines =
    {
        "Куб молчит. Но ты чувствуешь в нём дух Компаньон Куба.",
        "Где-то вдалеке слышится голос: \"The cake is a lie\".",
        "Ты трогаешь куб. Куб трогает тебя... эмоционально.",
        "В другой вселенной этот куб уже помогал Челл выбраться из лаборатории.",
        "Куб выглядит подозрительно. Возможно, он финальный босс туториала.",
        "Свежее мясо!",
        "Большой брат следит за тобой."
    };

    [SerializeField] private bool pickRandomLine = true;
    [SerializeField] private float dialogueAutoHideTime = 4f;

    public void Interact(PlayerController pc)
    {
        string line = description;

        if (dialogueLines != null && dialogueLines.Length > 0)
        {
            if (pickRandomLine)
            {
                int index = Random.Range(0, dialogueLines.Length);
                line = dialogueLines[index];
            }
            else
            {
                line = dialogueLines[0];
            }
        }

        if (SimpleDialogueUI.Instance != null)
        {
            SimpleDialogueUI.Instance.ShowMessage(line, dialogueAutoHideTime);
        }
        else
        {
            Debug.Log($"SimpleInteractable: {line}", this);
        }
    }

    public string GetDescription()
    {
        return description;
    }
}
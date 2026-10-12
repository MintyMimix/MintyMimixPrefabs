**HOW TO USE**
1. Place the PlayerTagManager prefab into your hierarchy.
2. Define any Single-type tags in the PlayerTagManager.
    - Single-type Tags have two states: ON and OFF. For example, if you wanted to have a 'Livestreaming Now!' label that you can show or hide, you'd want to use a Single-type Tag.
    2a. To make a Single-type tag, click the PlayerTagManager in the hierarchy.
    2b. Make a new entry for Tag Name, Tag Color, and Tag Display Above Head. Configure your settings accordingly.
    2c. Repeat step 2b for as many Single-type tags as you desire. Make sure that the arrays all match in length, or else you will encounter errors!
3. Assign any Group-type tags in the PlayerTagManager.
    - Group-type Tags are tags with multiple mutually exclusive states. For example, the 'Allow Headpats' group tag could have the states 'Headpats OK', 'Ask First', and 'No Headpats'.
    3a. To make a Group-type tag, click and drag a PlayerTagGroup prefab and place it underneath the PlayerTagManager.
    3b. Click on the PlayerTagGroup in the hierarchy. Rename the object and the group name.
    3c. In the Inspector, set the names, colors, and whether or not the tag should float above a player's head when ON for every state you want to define for this tag.
    3d. Click on the PlayerTagManager in the hierarchy. Create an entry under the Group-Type Tag array and assign the element to the PlayerTagGroup object we just modified.
    3e. Repeat Steps 3a-3d for as many Group-type tags as you desire.
4. Place the PlayerTagPreferences prefab somewhere in your scene.
    4a. Select all of the PlayerTagPreferenceButton objects in the hierarchy. Scroll to the OnClick() event on the button, and replace the "DELETETHIS" object with the PlayerTagManager you placed in Step 1.
    4b. Delete the "DELETETHIS" object in the hierarchy.
5. To allow a user to toggle a tag for themselves, place the PlayerTagButton somewhere in your scene.
    5a. Assign the PlayerTagManager from the hierarchy to the PlayerTagButton.
    5b. Assign the desired Button Type and tag to set.
        • Single: This button will toggle a Single-type tag ON/OFF.
        • Group Set Value: This button will a Group-type tag to a specific state.
        • Group Next Value: This button will a Group-type tag to the next state in its list. (For example, if a Group tag's states are 'Vanilla, Chocolate, Strawberry', and the current value is Vanilla, the value will become Chocolate, then Strawberry, then Vanilla again.)
    5c. Repeat step 5 for as many tags as you want to allow to be assigned.
    5d. For technical users who want to push the button remotely via UI or script, use the UdonBehaviour.Interact() method OR call the OnPush() public method on the PlayerTagButton script.
6. [OPTIONAL] To monitor a given tag and output the results somewhere (such as a text field or to expose it to script), place the PlayerTagListener in your scene.
    6a. Assign the PlayerTagManager from the hierarchy to the PlayerTagListener.
    6b. Select whether to listen for a Singletype or Group-type tag.
    6c. Select the tag to monitor, select the desired output type, and, if applicable, select which state(s) to monitor.
    6d. [OPTIONAL] Define what will separate the names/states. For example, if you want every name for the tag holder to be on a new line, use '\n' in the Output Name Separator field
    6e. [OPTIONAL] Assign the Output Text Element to the TMP_Text element you want the output to appear in.
# Installation

## Dependencies

Before you can import the AVICA package, you need to add some [scoped registries](https://docs.unity3d.com/Manual/upm-scoped.html) to your project. If you have no scoped registries already in your project, your `scopedRegistries` block in your `Packages/manifest.json` should look like this:

```
  "scopedRegistries": [
   {
      "name": "Keijiro",
      "url": "https://registry.npmjs.com",
      "scopes": [
        "jp.keijiro"
      ]
    }
  ]
```

Once that's done, check in Package Manager under `Packages: My Registries` and you should see two registries with a whole bunch of packages in. If you can, you're good to go!

## Importing

Importing the package is simple, just drag it into your Assets or Packages folder


# Setup

## Setting up AVICA Manager

This is a relatively simple process, simple add AVICAManager to your scene and set the `Platform Id`, `Partner Id` and `User Id`. Currently AVICAManager _is not_ persisted between scenes, so you will need to add it to any scenes you wish to use it in.

## Setting up the player

Add a `AVICAUser` component to your player object, for ease you should ideally make this a part of the player that the user will be able to move around the level - such as a player's head in VR, or the same Game Object as a character controller when not in VR. Failing that, you can set the `Proxy Transform` value to the part of the player object you wish to use to track which cameras they are visible to.

**Note:** It is advisable to set a unique layer for your players if you wish to use the "Complex Visibility Check" feature, so that you can ignore it from the Layer Mask. Otherwise the raycast will hit player colliders and we don't want that as it's checking for a clear path to the `AVICAUser` or its `Proxy Transform`.

To set the player's user ID, you can call `SetUserId` on the `AVICAUser` component via a script. see the `AVICAPluginSample` script in the sample for more information.

### Fields

`Proxy Transform` allows you to set a different `Transform` to be used for tracking when a user is visible in footage

`Use Complex Visibility Check` will enable raycasting to check if a `AVICAUser` is visible to a camera each frame or is obscured by an object. When disabled, a check will only be done that the player is within the bounds of the cameras Viewport.

`Layer Mask` is used by the Complex Visibility Check. Generally you only want this to include your level geometry, not your player colliders

`Trigger Interaction` is usually best left on `Ignore`, but decides whether the Complex Visibility Check should treat trigger volumes as an obstruction

## Setting up the cameras

It is advised to use cameras sparingly, as depending on the render size used, the user's system may not be able to cope with rendering that many viewpoints.

Add a `AVICACamera` script to each camera you wish to use, on the same object as the `Camera` component itself. Then add a list of events you wish to record events on this camera for.

AVICACamera will automatically choose between the "H264Default" preset and the "HevcNvidia" preset depending on whether a NVIDIA GPU is present.

To set the User ID of the camera (for player owned cameras, so they are always marked as present in footage from this camera) you can use the `SetOwnerUserId` method on the `AVICACamera` component. See the `AVICAPluginSample` script in the sample for more information.

**Note:** Do not add a `AVICACamera` to VR camera outputs, you should use a separate player point-of-view camera just for recording in a VR scenario.

### Fields

`Watched Events` is a list of `AVICAEventType` enums, defining what events should trigger footage on this camera

`Camera Type` sets the camera type, this is metadata for the cloud processing service

`Auto Enable Camera` will enable/disable the camera when you start/stop a session, so that the Unity Camera component is only rendering when you're recording

`Render To Screen` renders the camera output to the screen as well as to file. This is generally only used for local player's main camera

`Render Size` is the size to render this camera's output at. The higher the resolution, the more resource-intensive the recording will be

`Camera UDID` is automatically generated when you add the component, but can be manually changed if you wish to have more human-readable camera UDIDs. This value is used in the filename for footage and camera headers, so should not be set to too long a string.

## Recording

All recording is done through static methods on `AVICAManager` for ease of use. A full sample of starting/stopping sessions and events can be found in the `AVICAPluginSample` sample

**StartSession**

`AVICAManager.StartSession()` will begin a new session, returning the generated session ID if you wish to use it. If a session is already running, it will simply return the existing session ID

`AVICAManager.StopSession(float delay)` will stop the current session after a specified delay in seconds (which can be 0)

`StartEvent(AVICAEventType type, AVICACamera[] cameraOverride = null, string idOverride = null)` will start a persistent event, you can also override which cameras to use for this event, and can override the event ID if you so choose (otherwise one will be generated). The event ID will be returned, you should store this as you'll need it to be able to stop the event.

`StopEvent(string eventId)` will stop the event specified by the `eventId` parameter you got from `StartEvent()`

`CaptureEvent(AVICAEventType type, float secondsBefore, float secondsAfter, AVICACamera[] cameraOverride = null, string idOverride = null)` will trigger an "Instant Capture" event, capturing the last `secondsBefore` seconds and the next `secondsAfter` seconds. You can once again override the cameras used and the event ID.

**Note:** you will see a delay in when the log will show an instant replay capture, as the event is not committed to the event log until the `secondsAfter` time has elapsed.

# Storage

All files are stored in a `recordings` folder in the game folder, with each session being its own folder in the form of `Session_<id>`. Inside this folder you will find the main `events.json` folder, plus a `Footage` folder containing the MP4 and header JSON for each camera.

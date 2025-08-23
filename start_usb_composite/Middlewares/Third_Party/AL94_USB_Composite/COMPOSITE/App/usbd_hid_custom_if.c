/**
  ******************************************************************************
  * @file           : usbd_custom_hid_if.c
  * @version        : v1.0_Cube
  * @brief          : USB Device Custom HID interface file.
  ******************************************************************************
  * This notice applies to any and all portions of this file
  * that are not between comment pairs USER CODE BEGIN and
  * USER CODE END. Other portions of this file, whether
  * inserted by the user or by software development tools
  * are owned by their respective copyright owners.
  *
  * Copyright (c) 2018 STMicroelectronics International N.V.
  * All rights reserved.
  *
  * Redistribution and use in source and binary forms, with or without
  * modification, are permitted, provided that the following conditions are met:
  *
  * 1. Redistribution of source code must retain the above copyright notice,
  *    this list of conditions and the following disclaimer.
  * 2. Redistributions in binary form must reproduce the above copyright notice,
  *    this list of conditions and the following disclaimer in the documentation
  *    and/or other materials provided with the distribution.
  * 3. Neither the name of STMicroelectronics nor the names of other
  *    contributors to this software may be used to endorse or promote products
  *    derived from this software without specific written permission.
  * 4. This software, including modifications and/or derivative works of this
  *    software, must execute solely and exclusively on microcontroller or
  *    microprocessor devices manufactured by or for STMicroelectronics.
  * 5. Redistribution and use of this software other than as permitted under
  *    this license is void and will automatically terminate your rights under
  *    this license.
  *
  * THIS SOFTWARE IS PROVIDED BY STMICROELECTRONICS AND CONTRIBUTORS "AS IS"
  * AND ANY EXPRESS, IMPLIED OR STATUTORY WARRANTIES, INCLUDING, BUT NOT
  * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
  * PARTICULAR PURPOSE AND NON-INFRINGEMENT OF THIRD PARTY INTELLECTUAL PROPERTY
  * RIGHTS ARE DISCLAIMED TO THE FULLEST EXTENT PERMITTED BY LAW. IN NO EVENT
  * SHALL STMICROELECTRONICS OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
  * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
  * LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA,
  * OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
  * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
  * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
  * EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
  *
  ******************************************************************************
  */

/* Includes ------------------------------------------------------------------*/
#include "usbd_hid_custom_if.h"

/* USER CODE BEGIN INCLUDE */

/* USER CODE END INCLUDE */

/* Private typedef -----------------------------------------------------------*/
/* Private define ------------------------------------------------------------*/
/* Private macro -------------------------------------------------------------*/

/* USER CODE BEGIN PV */
/* Private variables ---------------------------------------------------------*/
//uint8_t buffer[0x40];

extern HID_FrameFIFO_t g_HIDFrameFIFO_Receive;

/* USER CODE END PV */

/** @addtogroup STM32_USB_OTG_DEVICE_LIBRARY
  * @brief Usb device.
  * @{
  */

/** @addtogroup USBD_CUSTOM_HID
  * @{
  */

/** @defgroup USBD_CUSTOM_HID_Private_TypesDefinitions USBD_CUSTOM_HID_Private_TypesDefinitions
  * @brief Private types.
  * @{
  */

/* USER CODE BEGIN PRIVATE_TYPES */

/* USER CODE END PRIVATE_TYPES */

/**
  * @}
  */

/** @defgroup USBD_CUSTOM_HID_Private_Defines USBD_CUSTOM_HID_Private_Defines
  * @brief Private defines.
  * @{
  */

/* USER CODE BEGIN PRIVATE_DEFINES */

/* USER CODE END PRIVATE_DEFINES */

/**
  * @}
  */

/** @defgroup USBD_CUSTOM_HID_Private_Macros USBD_CUSTOM_HID_Private_Macros
  * @brief Private macros.
  * @{
  */

/* USER CODE BEGIN PRIVATE_MACRO */

/* USER CODE END PRIVATE_MACRO */

/**
  * @}
  */

/** @defgroup USBD_CUSTOM_HID_Private_Variables USBD_CUSTOM_HID_Private_Variables
  * @brief Private variables.
  * @{
  */

/** Usb HID report descriptor. */
__ALIGN_BEGIN static uint8_t CUSTOM_HID_ReportDesc[USBD_CUSTOM_HID_REPORT_DESC_SIZE] __ALIGN_END =
    {
        /* USER CODE BEGIN 0 */
#if 1
    		0x06, 0x00, 0xff, //Usage Page(Undefined )
        0x09, 0x01,       // USAGE (Undefined)
        0xa1, 0x01,       // COLLECTION (Application)
        0x15, 0x00,       //   LOGICAL_MINIMUM (0)
        0x26, 0xff, 0x00, //   LOGICAL_MAXIMUM (255)
        0x75, 0x08,       //   REPORT_SIZE (8)
        //0x95, 0x40,       //   REPORT_COUNT (64)
		 0x95, 0x20,       //   REPORT_COUNT (64)
        0x09, 0x01,       //   USAGE (Undefined)
        0x81, 0x02,       //   INPUT (Data,Var,Abs)
//        0x95, 0x40,       //   REPORT_COUNT (64)
		 0x95, 0x20,       //   REPORT_COUNT (64)
        0x09, 0x01,       //   USAGE (Undefined)
        0x91, 0x02,       //   OUTPUT (Data,Var,Abs)
        0x95, 0x01,       //   REPORT_COUNT (1)
        0x09, 0x01,       //   USAGE (Undefined)
        0xb1, 0x02,       //   FEATURE (Data,Var,Abs)
#else

    		0x06, 0x00, 0xFF,       // Usage Page = 0xFF00 (Vendor Defined Page 1)
    		    0x09, 0x01,             // Usage (Vendor Usage 1)
    		    0xA1, 0x01,             // Collection (Application)
    		    0x19, 0x01,             //      Usage Minimum
    		    0x29, 0x40,             //      Usage Maximum     //64 input usages total (0x01 to 0x40)
    		    0x15, 0x01,             //      Logical Minimum (data bytes in the report may have minimum value = 0x00)
    		    0x25, 0x40,                //      Logical Maximum (data bytes in the report may have maximum value = 0x00FF = unsigned 255)
    		    0x75, 0x08,             //      Report Size: 8-bit field size
    		    0x95, 0x40,             //      Report Count: Make sixty-four 8-bit fields (the next time the parser hits an "Input", "Output", or "Feature" item)
    		    0x81, 0x00,             //      Input (Data, Array, Abs): Instantiates input packet fields based on the above report size, count, logical min/max, and usage.
    		    0x19, 0x01,             //      Usage Minimum
    		    0x29, 0x40,             //      Usage Maximum     //64 output usages total (0x01 to 0x40)
    		    0x91, 0x00,             //      Output (Data, Array, Abs): Instantiates output packet fields.  Uses same report size and count as "Input" fields, since nothing new/different was specified to the parser since the "Input" item.
#endif
        /* USER CODE END 0 */
        0xC0 /*     END_COLLECTION	             */
};

/* USER CODE BEGIN PRIVATE_VARIABLES */

/* USER CODE END PRIVATE_VARIABLES */

/**
  * @}
  */

/** @defgroup USBD_CUSTOM_HID_Exported_Variables USBD_CUSTOM_HID_Exported_Variables
  * @brief Public variables.
  * @{
  */
extern USBD_HandleTypeDef hUsbDevice;

/* USER CODE BEGIN EXPORTED_VARIABLES */

/* USER CODE END EXPORTED_VARIABLES */
/**
  * @}
  */

/** @defgroup USBD_CUSTOM_HID_Private_FunctionPrototypes USBD_CUSTOM_HID_Private_FunctionPrototypes
  * @brief Private functions declaration.
  * @{
  */

static int8_t CUSTOM_HID_Init(void);
static int8_t CUSTOM_HID_DeInit(void);
static int8_t CUSTOM_HID_OutEvent(uint8_t *data, uint16_t len);

/**
  * @}
  */

USBD_CUSTOM_HID_ItfTypeDef USBD_CustomHID_fops = {CUSTOM_HID_ReportDesc,
                                                  CUSTOM_HID_Init,
                                                  CUSTOM_HID_DeInit,
                                                  CUSTOM_HID_OutEvent};

/** @defgroup USBD_CUSTOM_HID_Private_Functions USBD_CUSTOM_HID_Private_Functions
  * @brief Private functions.
  * @{
  */

/* Private functions ---------------------------------------------------------*/

/**
  * @brief  Initializes the CUSTOM HID media low layer
  * @retval USBD_OK if all operations are OK else USBD_FAIL
  */
static int8_t CUSTOM_HID_Init(void)
{
  /* USER CODE BEGIN 4 */
  return (USBD_OK);
  /* USER CODE END 4 */
}

/**
  * @brief  DeInitializes the CUSTOM HID media low layer
  * @retval USBD_OK if all operations are OK else USBD_FAIL
  */
static int8_t CUSTOM_HID_DeInit(void)
{
  /* USER CODE BEGIN 5 */
  return (USBD_OK);
  /* USER CODE END 5 */
}

/**
  * @brief  Manage the CUSTOM HID class events
  * @param  event_idx: Event index
  * @param  state: Event state
  * @retval USBD_OK if all operations are OK else USBD_FAIL
  */
static int8_t CUSTOM_HID_OutEvent(uint8_t *data, uint16_t len)
{
  /* USER CODE BEGIN 6 */
	HID_Frame_Write(&g_HIDFrameFIFO_Receive,data);
  //memcpy(buffer, state, 0x40);
  //USBD_CUSTOM_HID_SendReport(&hUsbDevice, (uint8_t *)buffer, 0x40);
  return (USBD_OK);
  /* USER CODE END 6 */
}

/* USER CODE BEGIN 7 */

//uint8_t HID_Frame_Write(HID_FrameFIFO_t *fifo, uint8_t *data)
//{
//    uint8_t nextHead = (fifo->head + 1) % HID_FRAME_BUFFER_SIZE;
//
//    // Kiểm tra tràn bộ đệm
//    if (nextHead == fifo->tail) {
//        // Buffer đầy
//    	GPIOA->ODR ^= (1 << 7);
//        return 0;
//    }
//
//    memcpy(fifo->frame[fifo->head], data, HID_FRAME_SIZE);
//    fifo->head = nextHead;
//    return 1;
//}
//
//
//
//
//uint8_t HID_Frame_Read(HID_FrameFIFO_t *fifo, uint8_t *dest_buf) {
//    if (fifo->head == fifo->tail) {
//        return 0;  // Không có frame
//    }
//
//    memcpy(dest_buf, fifo->frame[fifo->tail], HID_FRAME_SIZE);
//    fifo->tail = (fifo->tail + 1) % HID_FRAME_BUFFER_SIZE;
//    return 1;
//}
//
//
//uint8_t HID_Frame_ReadAndSend(HID_FrameFIFO_t *fifo, uint8_t *dest_buf)
//{
//    // Kiểm tra có frame không
//    if(fifo->head == fifo->tail)
//        return 0;  // FIFO rỗng
//
//    // Copy frame ra buffer tạm
//    memcpy(dest_buf, fifo->frame[fifo->tail], HID_FRAME_SIZE);
//
//    // Thử gửi USB
//    if(USBD_CUSTOM_HID_SendReport(&hUsbDevice, dest_buf, HID_FRAME_SIZE) == USBD_OK)
//    {
//        // Gửi thành công → đánh dấu frame đã đọc
//        fifo->tail = (fifo->tail + 1) % HID_FRAME_BUFFER_SIZE;
//        return 1;
//    }
//    else
//    {
//        // USB bận → không thay đổi tail, frame sẽ gửi lại lần sau
//        return 2;  // Trạng thái gửi chưa thành công
//    }
//}
//
///**
// * HID_Frame_ReadAndSendCan
// *  - Đọc frame từ FIFO
// *  - Gọi hàm gửi CAN tương ứng
// *  - Nếu gửi thành công → đánh dấu frame đã đọc (tail tiến)
// *  - Nếu gửi không thành công → tail giữ nguyên, sẽ gửi lại lần sau
// *
// * Trả về:
// *  0: FIFO rỗng
// *  1: frame đã gửi thành công
// *  2: frame chưa gửi (CAN bận hoặc lỗi)
// */
//uint8_t HID_Frame_ReadAndSendCan(HID_FrameFIFO_t *fifo)
//{
//    uint8_t frame[HID_FRAME_SIZE];
//
//    // Kiểm tra FIFO rỗng
//    if(fifo->head == fifo->tail)
//        return 0;
//
//    // Copy frame ra buffer tạm
//    memcpy(frame, fifo->frame[fifo->tail], HID_FRAME_SIZE);
//
//    // Gọi hàm gửi CAN tương ứng
//    uint8_t sendResult = FuncSendCanArray[frame[0]](frame);
//
//    if(sendResult) {
//        // Gửi thành công → đánh dấu đã đọc
//        fifo->tail = (fifo->tail + 1) % HID_FRAME_BUFFER_SIZE;
//        return 1;
//    } else {
//        // Gửi chưa thành công → tail giữ nguyên
//        return 2;
//    }
//}
/**
  * @brief  Send the report to the Host
  * @param  report: The report to be sent
  * @param  len: The report length
  * @retval USBD_OK if all operations are OK else USBD_FAIL
  */
/*
static int8_t USBD_CUSTOM_HID_SendReport(uint8_t *report, uint16_t len)
{
  return USBD_CUSTOM_HID_SendReport(&hUsbDevice, report, len);
}
*/
/* USER CODE END 7 */

/* USER CODE BEGIN PRIVATE_FUNCTIONS_IMPLEMENTATION */

/* USER CODE END PRIVATE_FUNCTIONS_IMPLEMENTATION */
/**
  * @}
  */

/**
  * @}
  */

/**
  * @}
  */

/************************ (C) COPYRIGHT STMicroelectronics *****END OF FILE****/
